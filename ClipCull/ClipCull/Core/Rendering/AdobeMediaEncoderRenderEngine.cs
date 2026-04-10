using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClipCull.Core.Rendering
{
    public class AdobeMediaEncoderRenderEngine : IRenderEngine
    {
        public string Name => "Adobe Media Encoder";

        public RenderEngineType EngineType => RenderEngineType.AdobeMediaEncoder;

        public IReadOnlyList<VideoCodec> SupportedVideoCodecs { get; } =
            new List<VideoCodec> { VideoCodec.H264, VideoCodec.H265, VideoCodec.ProRes, VideoCodec.DNxHR }.AsReadOnly();

        public IReadOnlyList<AudioCodec> SupportedAudioCodecs { get; } =
            new List<AudioCodec> { AudioCodec.AAC, AudioCodec.PCM, AudioCodec.None }.AsReadOnly();

        public IReadOnlyList<ContainerFormat> SupportedContainerFormats { get; } =
            new List<ContainerFormat> { ContainerFormat.MP4, ContainerFormat.MOV }.AsReadOnly();

        public IReadOnlyList<HardwareAcceleration> SupportedHardwareAcceleration { get; } =
            new List<HardwareAcceleration> { HardwareAcceleration.Auto }.AsReadOnly();

        private static readonly string[] CommonInstallPaths = new[]
        {
            @"C:\Program Files\Adobe\Adobe Media Encoder 2025",
            @"C:\Program Files\Adobe\Adobe Media Encoder 2024",
            @"C:\Program Files\Adobe\Adobe Media Encoder 2023",
            @"C:\Program Files\Adobe\Adobe Media Encoder CC 2022",
            @"C:\Program Files\Adobe\Adobe Media Encoder CC 2021",
        };

        public Task<bool> IsAvailableAsync()
        {
            string path = GetAMEExecutablePath();
            return Task.FromResult(path != null);
        }

        public string GetStatusDescription()
        {
            string path = GetAMEExecutablePath();
            if (path != null)
                return $"Installed at {path}";

            return "Adobe Media Encoder not found. Please install AME or set the path in settings.";
        }

        public async Task<string> RenderAsync(
            RenderJobInfo job,
            RenderSettings settings,
            string outputDirectory,
            bool overwrite,
            Action<RenderProgress> progressCallback,
            CancellationToken cancellationToken = default)
        {
            string amePath = GetAMEExecutablePath();
            if (amePath == null)
                throw new FileNotFoundException("Adobe Media Encoder executable not found.");

            string outputFile = Path.Combine(outputDirectory, job.OutputName);

            if (File.Exists(outputFile) && !overwrite)
            {
                Trace.WriteLine($"Output file {outputFile} already exists and overwrite is false, skipping.");
                return outputFile;
            }

            if (File.Exists(outputFile) && overwrite)
                File.Delete(outputFile);

            double totalDurationMs = (job.EndTime - job.StartTime).TotalMilliseconds;

            // AME headless CLI rendering
            // Format: "Adobe Media Encoder.exe" -batchrender -source "input" -dest "output"
            string arguments = BuildAMEArgs(job, settings, outputFile);
            Trace.WriteLine($"Running: {amePath} {arguments}");

            var stopwatch = Stopwatch.StartNew();
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = amePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = new Process { StartInfo = processStartInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdOutBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"AME: {e.Data}");

                    var progress = ParseAMEProgress(e.Data, totalDurationMs, stopwatch.Elapsed);
                    if (progress != null)
                        progressCallback?.Invoke(progress);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"AME Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

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
                    throw new Exception($"Adobe Media Encoder failed with exit code {process.ExitCode}: {error}");
                Logger.LogDebug("AME reported error but output file was created.", "Adobe Media Encoder");
            }

            if (!File.Exists(outputFile))
                throw new Exception("Adobe Media Encoder completed but output file was not created.");

            return outputFile;
        }

        private string BuildAMEArgs(RenderJobInfo job, RenderSettings settings, string outputFile)
        {
            var args = new List<string>
            {
                "-batchrender",
                $"-source \"{job.VideoFile}\"",
                $"-dest \"{outputFile}\""
            };

            // Trim parameters
            args.Add($"-starttime {job.StartTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            args.Add($"-endtime {job.EndTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");

            // Format/codec selection
            string format = GetAMEFormat(settings.VideoCodec, settings.ContainerFormat);
            if (!string.IsNullOrEmpty(format))
                args.Add($"-format \"{format}\"");

            return string.Join(" ", args);
        }

        private static string GetAMEFormat(VideoCodec videoCodec, ContainerFormat container)
        {
            // AME uses preset names for format selection
            return (videoCodec, container) switch
            {
                (VideoCodec.H264, ContainerFormat.MP4) => "H.264",
                (VideoCodec.H265, ContainerFormat.MP4) => "HEVC (H.265)",
                (VideoCodec.ProRes, ContainerFormat.MOV) => "Apple ProRes",
                (VideoCodec.DNxHR, ContainerFormat.MOV) => "DNxHR",
                _ => "H.264"
            };
        }

        private static RenderProgress ParseAMEProgress(string line, double totalDurationMs, TimeSpan elapsed)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            // AME progress parsing - format varies by version
            // Common pattern: "Encoding: XX%" or progress percentage in output
            var percentMatch = Regex.Match(line, @"(\d+(?:\.\d+)?)\s*%");
            if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, CultureInfo.InvariantCulture, out double percent))
            {
                var progress = new RenderProgress
                {
                    Percentage = percent,
                    Elapsed = elapsed,
                    RawOutput = line
                };

                if (percent > 0)
                {
                    double msPerPercent = elapsed.TotalMilliseconds / percent;
                    double remainingPercent = 100.0 - percent;
                    progress.ETA = TimeSpan.FromMilliseconds(msPerPercent * remainingPercent);
                }

                return progress;
            }

            return null;
        }

        private string GetAMEExecutablePath()
        {
            // Check user-configured path first
            string settingsPath = SettingsHandler.Settings.AdobeMediaEncoderPath;
            if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
                return settingsPath;

            // Search common install paths
            foreach (string basePath in CommonInstallPaths)
            {
                if (!Directory.Exists(basePath))
                    continue;

                string[] possibleExes =
                {
                    Path.Combine(basePath, "Adobe Media Encoder.exe"),
                    Path.Combine(basePath, "AdobeMediaEncoder.exe"),
                };

                string found = possibleExes.FirstOrDefault(File.Exists);
                if (found != null)
                    return found;
            }

            // Try registry discovery
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Adobe\Adobe Media Encoder");
                if (key != null)
                {
                    string installPath = key.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(installPath))
                    {
                        string exe = Path.Combine(installPath, "Adobe Media Encoder.exe");
                        if (File.Exists(exe))
                            return exe;
                    }
                }
            }
            catch
            {
                // Registry access may fail on non-Windows or without permissions
            }

            return null;
        }
    }
}
