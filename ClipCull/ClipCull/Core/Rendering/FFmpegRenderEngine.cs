using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClipCull.Core.Rendering
{
    public class FFmpegRenderEngine : IRenderEngine
    {
        public string Name => "FFmpeg";

        public RenderEngineType EngineType => RenderEngineType.FFmpeg;

        public IReadOnlyList<VideoCodec> SupportedVideoCodecs { get; } =
            new List<VideoCodec> { VideoCodec.H264, VideoCodec.H265, VideoCodec.ProRes, VideoCodec.DNxHR, VideoCodec.VP9, VideoCodec.AV1 }.AsReadOnly();

        public IReadOnlyList<AudioCodec> SupportedAudioCodecs { get; } =
            new List<AudioCodec> { AudioCodec.AAC, AudioCodec.PCM, AudioCodec.FLAC, AudioCodec.Opus, AudioCodec.None }.AsReadOnly();

        public IReadOnlyList<ContainerFormat> SupportedContainerFormats { get; } =
            new List<ContainerFormat> { ContainerFormat.MP4, ContainerFormat.MOV, ContainerFormat.MKV, ContainerFormat.WebM }.AsReadOnly();

        public IReadOnlyList<HardwareAcceleration> SupportedHardwareAcceleration { get; } =
            new List<HardwareAcceleration> { HardwareAcceleration.None, HardwareAcceleration.NVENC, HardwareAcceleration.QSV, HardwareAcceleration.AMF, HardwareAcceleration.Auto }.AsReadOnly();

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(File.Exists(Globals.FFmpegPath));
        }

        public string GetStatusDescription()
        {
            if (File.Exists(Globals.FFmpegPath))
                return $"Installed at {Globals.FFmpegPath}";

            return "FFmpeg not found. Please place ffmpeg.exe in the External folder.";
        }

        public async Task<string> RenderAsync(
            RenderJobInfo job,
            RenderSettings settings,
            string outputDirectory,
            bool overwrite,
            Action<RenderProgress> progressCallback,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(Globals.FFmpegPath))
                throw new FileNotFoundException("FFmpeg executable not found.", Globals.FFmpegPath);

            string outputFile = Path.Combine(outputDirectory, job.OutputName);

            if (File.Exists(outputFile) && !overwrite)
            {
                Trace.WriteLine($"Output file {outputFile} already exists and overwrite is false, skipping.");
                return outputFile;
            }

            // Get total duration for progress calculation
            double totalDurationMs = (job.EndTime - job.StartTime).TotalMilliseconds;

            string arguments = BuildFFmpegArgs(job, settings, outputFile, overwrite);
            Trace.WriteLine($"Running: {Globals.FFmpegPath} {arguments}");

            var stopwatch = Stopwatch.StartNew();
            var stdErrBuffer = new StringBuilder();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = Globals.FFmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = new Process { StartInfo = processStartInfo };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"FFmpeg: {e.Data}");

                    var progress = ParseFFmpegProgress(e.Data, totalDurationMs, stopwatch.Elapsed);
                    if (progress != null)
                        progressCallback?.Invoke(progress);
                }
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    Trace.WriteLine($"FFmpeg stdout: {e.Data}");
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            // Register cancellation
            using var registration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); }
                catch { /* ignore */ }
            });

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string error = stdErrBuffer.ToString().Trim();
                if (!File.Exists(outputFile))
                    throw new Exception($"FFmpeg failed with exit code {process.ExitCode}: {error}");
                Logger.LogDebug("FFmpeg reported error but output file was created.", "FFmpeg");
            }

            return outputFile;
        }

        private string BuildFFmpegArgs(RenderJobInfo job, RenderSettings settings, string outputFile, bool overwrite)
        {
            var args = new List<string>();

            // Overwrite
            if (overwrite)
                args.Add("-y");
            else
                args.Add("-n");

            // Input with seek (fast seek before -i)
            args.Add($"-ss {job.StartTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            args.Add($"-i \"{job.VideoFile}\"");

            // Duration
            double duration = (job.EndTime - job.StartTime).TotalSeconds;
            args.Add($"-t {duration.ToString("F3", CultureInfo.InvariantCulture)}");

            // Video codec
            string videoCodecArg = GetVideoCodecArg(settings.VideoCodec, settings.HardwareAcceleration);
            args.Add($"-c:v {videoCodecArg}");

            // Quality settings
            switch (settings.QualityMode)
            {
                case QualityMode.CRF:
                    args.Add($"-crf {settings.Quality}");
                    break;
                case QualityMode.CBR:
                    if (settings.Bitrate > 0)
                        args.Add($"-b:v {settings.Bitrate}k");
                    break;
                case QualityMode.VBR:
                    if (settings.Bitrate > 0)
                        args.Add($"-b:v {settings.Bitrate}k");
                    break;
            }

            // Preset (for x264/x265)
            if (!string.IsNullOrEmpty(settings.Preset) &&
                (settings.VideoCodec == VideoCodec.H264 || settings.VideoCodec == VideoCodec.H265))
            {
                args.Add($"-preset {settings.Preset}");
            }

            // Build video filters
            var filters = new List<string>();

            // Resolution
            if (settings.OutputWidth > 0 && settings.OutputHeight > 0)
            {
                filters.Add($"scale={settings.OutputWidth}:{settings.OutputHeight}");
            }

            // Rotation
            if (job.Rotation != 0)
            {
                string rotationFilter = GetRotationFilter(job.Rotation);
                if (!string.IsNullOrEmpty(rotationFilter))
                    filters.Add(rotationFilter);
            }

            if (filters.Count > 0)
                args.Add($"-vf \"{string.Join(",", filters)}\"");

            // Audio
            if (settings.AudioCodec == AudioCodec.None)
            {
                args.Add("-an");
            }
            else
            {
                string audioCodecArg = GetAudioCodecArg(settings.AudioCodec);
                args.Add($"-c:a {audioCodecArg}");
                args.Add($"-b:a {settings.AudioBitrate}k");
            }

            // Metadata: strip rotation since we handle it in filters
            if (job.Rotation != 0)
                args.Add("-metadata:s:v:0 rotate=0");

            // Output
            args.Add($"\"{outputFile}\"");

            return string.Join(" ", args);
        }

        private static string GetVideoCodecArg(VideoCodec codec, HardwareAcceleration hwAccel)
        {
            // GPU-accelerated encoders
            if (hwAccel == HardwareAcceleration.NVENC || hwAccel == HardwareAcceleration.Auto)
            {
                return codec switch
                {
                    VideoCodec.H264 => hwAccel == HardwareAcceleration.NVENC ? "h264_nvenc" : "libx264",
                    VideoCodec.H265 => hwAccel == HardwareAcceleration.NVENC ? "hevc_nvenc" : "libx265",
                    _ => GetSoftwareCodecArg(codec)
                };
            }

            if (hwAccel == HardwareAcceleration.QSV)
            {
                return codec switch
                {
                    VideoCodec.H264 => "h264_qsv",
                    VideoCodec.H265 => "hevc_qsv",
                    _ => GetSoftwareCodecArg(codec)
                };
            }

            if (hwAccel == HardwareAcceleration.AMF)
            {
                return codec switch
                {
                    VideoCodec.H264 => "h264_amf",
                    VideoCodec.H265 => "hevc_amf",
                    _ => GetSoftwareCodecArg(codec)
                };
            }

            return GetSoftwareCodecArg(codec);
        }

        private static string GetSoftwareCodecArg(VideoCodec codec)
        {
            return codec switch
            {
                VideoCodec.H264 => "libx264",
                VideoCodec.H265 => "libx265",
                VideoCodec.ProRes => "prores_ks",
                VideoCodec.DNxHR => "dnxhd",
                VideoCodec.VP9 => "libvpx-vp9",
                VideoCodec.AV1 => "libaom-av1",
                _ => "libx264"
            };
        }

        private static string GetAudioCodecArg(AudioCodec codec)
        {
            return codec switch
            {
                AudioCodec.AAC => "aac",
                AudioCodec.PCM => "pcm_s16le",
                AudioCodec.FLAC => "flac",
                AudioCodec.Opus => "libopus",
                _ => "aac"
            };
        }

        private static string GetRotationFilter(int rotation)
        {
            return rotation switch
            {
                90 => "transpose=1",
                180 => "transpose=1,transpose=1",
                270 => "transpose=2",
                _ => null
            };
        }

        private static RenderProgress ParseFFmpegProgress(string line, double totalDurationMs, TimeSpan elapsed)
        {
            // FFmpeg progress line format: frame= 1234 fps= 60.0 q=28.0 size=   12345kB time=00:01:23.45 bitrate= 1234.5kbits/s speed=2.5x
            if (!line.Contains("time="))
                return null;

            var progress = new RenderProgress
            {
                RawOutput = line,
                Elapsed = elapsed
            };

            // Parse time
            var timeMatch = Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
            if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out TimeSpan currentTime))
            {
                double currentMs = currentTime.TotalMilliseconds;
                if (totalDurationMs > 0)
                    progress.Percentage = Math.Min(100.0, currentMs / totalDurationMs * 100.0);

                // ETA calculation
                if (progress.Percentage > 0)
                {
                    double remainingMs = totalDurationMs - currentMs;
                    double msPerPercent = elapsed.TotalMilliseconds / progress.Percentage;
                    double remainingPercent = 100.0 - progress.Percentage;
                    progress.ETA = TimeSpan.FromMilliseconds(msPerPercent * remainingPercent);
                }
            }

            // Parse frame
            var frameMatch = Regex.Match(line, @"frame=\s*(\d+)");
            if (frameMatch.Success && int.TryParse(frameMatch.Groups[1].Value, out int frame))
                progress.CurrentFrame = frame;

            // Parse fps
            var fpsMatch = Regex.Match(line, @"fps=\s*([0-9.]+)");
            if (fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, CultureInfo.InvariantCulture, out double fps))
                progress.FPS = fps;

            // Parse speed
            var speedMatch = Regex.Match(line, @"speed=\s*([0-9.]+)x");
            if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, CultureInfo.InvariantCulture, out double speed))
                progress.Speed = speed;

            return progress;
        }
    }
}
