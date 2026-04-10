using ClipCull.Core.Gyroflow;
using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static ClipCull.Core.Gyroflow.GyroflowSubclipExtractor;

namespace ClipCull.Core.Rendering
{
    public class GyroflowRenderEngine : IRenderEngine
    {
        public string Name => "Gyroflow (Stabilization)";

        public RenderEngineType EngineType => RenderEngineType.Gyroflow;

        public IReadOnlyList<VideoCodec> SupportedVideoCodecs { get; } =
            new List<VideoCodec> { VideoCodec.H264, VideoCodec.H265, VideoCodec.ProRes }.AsReadOnly();

        public IReadOnlyList<AudioCodec> SupportedAudioCodecs { get; } =
            new List<AudioCodec> { AudioCodec.AAC, AudioCodec.PCM, AudioCodec.None }.AsReadOnly();

        public IReadOnlyList<ContainerFormat> SupportedContainerFormats { get; } =
            new List<ContainerFormat> { ContainerFormat.MP4 }.AsReadOnly();

        public IReadOnlyList<HardwareAcceleration> SupportedHardwareAcceleration { get; } =
            new List<HardwareAcceleration> { HardwareAcceleration.Auto }.AsReadOnly();

        public Task<bool> IsAvailableAsync()
        {
            string path = SettingsHandler.Settings.GyroflowPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return Task.FromResult(true);

            string discovered = GyroflowHelper.GetGyroflowInstallationPath();
            return Task.FromResult(!string.IsNullOrEmpty(discovered) && File.Exists(discovered));
        }

        public string GetStatusDescription()
        {
            string path = SettingsHandler.Settings.GyroflowPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return $"Installed at {path}";

            string discovered = GyroflowHelper.GetGyroflowInstallationPath();
            if (!string.IsNullOrEmpty(discovered) && File.Exists(discovered))
                return $"Auto-discovered at {discovered}";

            return "Not found. Please install Gyroflow or set the path in settings.";
        }

        public async Task<string> RenderAsync(
            RenderJobInfo job,
            RenderSettings settings,
            string outputDirectory,
            bool overwrite,
            Action<RenderProgress> progressCallback,
            CancellationToken cancellationToken = default)
        {
            string settingsFile = SettingsHandler.Settings.GyroflowSettingsPath;
            var extractor = new GyroflowSubclipExtractor(outputDirectory, settingsFile);

            // Convert RenderJobInfo to SubclipInfo for the existing extractor
            var subclipInfo = ToSubclipInfo(job);

            // Translate rotation for Gyroflow (Gyroflow uses inverse rotation)
            if (SettingsHandler.Settings.GyroflowRenderWithRotation)
            {
                subclipInfo.Rotation = TranslateRotationForGyroflow(job.Rotation);
            }
            else
            {
                subclipInfo.Rotation = 0;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string result = await extractor.ExtractSubclip(
                subclipInfo,
                overwrite,
                settings.ParallelRenders,
                rawOutput =>
                {
                    var progress = ParseGyroflowProgress(rawOutput, stopwatch.Elapsed);
                    progressCallback?.Invoke(progress);
                });

            return result;
        }

        private static SubclipInfo ToSubclipInfo(RenderJobInfo job)
        {
            return new SubclipInfo
            {
                VideoFile = job.VideoFile,
                StartTime = job.StartTime,
                EndTime = job.EndTime,
                OutputName = job.OutputName,
                Rotation = job.Rotation
            };
        }

        private static int TranslateRotationForGyroflow(int rotation)
        {
            return rotation switch
            {
                90 => 270,
                180 => 180,
                270 => 90,
                _ => 0
            };
        }

        private static RenderProgress ParseGyroflowProgress(string rawOutput, TimeSpan elapsed)
        {
            var progress = new RenderProgress
            {
                RawOutput = rawOutput,
                Elapsed = elapsed
            };

            if (string.IsNullOrEmpty(rawOutput))
                return progress;

            // Parse Gyroflow progress format: "Elapsed: HH:MM:SS N/M ETA Xs"
            var match = Regex.Match(rawOutput,
                @"Elapsed:\s*(\d{2}:\d{2}:\d{2}).*?(\d+)/(\d+)\s+ETA\s*([0-9.]+)s",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                if (int.TryParse(match.Groups[2].Value, out int current) &&
                    int.TryParse(match.Groups[3].Value, out int total) &&
                    total > 0)
                {
                    progress.CurrentFrame = current;
                    progress.TotalFrames = total;
                    progress.Percentage = (double)current / total * 100.0;
                }

                if (double.TryParse(match.Groups[4].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double etaSeconds))
                {
                    progress.ETA = TimeSpan.FromSeconds(etaSeconds);
                }

                if (TimeSpan.TryParse(match.Groups[1].Value, out TimeSpan parsedElapsed))
                {
                    progress.Elapsed = parsedElapsed;
                }
            }

            return progress;
        }
    }
}
