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

namespace ClipCull.Core.Proxy
{
    /// <summary>
    /// Generates lightweight proxy files for a batch of source videos using FFmpeg, optimised for
    /// speed (GPU AMF encoding with a fast software fallback). Reports per-file and overall progress
    /// with a duration-weighted time estimate.
    /// </summary>
    public class ProxyGenerator
    {
        private static readonly Regex TimeRegex = new Regex(@"time=(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled);
        private static readonly Regex SpeedRegex = new Regex(@"speed=\s*([0-9.]+)x", RegexOptions.Compiled);

        /// <summary>
        /// Processes every file in <paramref name="files"/>, writing a proxy next to each. Progress is
        /// reported through <paramref name="progress"/> on a background thread; callers should marshal
        /// UI updates to the dispatcher themselves.
        /// </summary>
        public async Task<ProxyBatchResult> GenerateAsync(
            IReadOnlyList<string> files,
            ProxyOptions options,
            IProgress<ProxyBatchProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new ProxyBatchResult();

            if (!File.Exists(Globals.FFmpegPath))
            {
                result.Errors.Add($"FFmpeg not found at {Globals.FFmpegPath}");
                result.Failed = files?.Count ?? 0;
                return result;
            }

            // Decide up front which files will actually be processed so the time estimate only
            // accounts for real work (skipped files contribute nothing).
            var work = new List<(string File, double Duration)>();
            var skipped = new List<string>();
            foreach (var file in files)
            {
                if (options.SkipExisting && ProxyService.HasProxy(file))
                    skipped.Add(file);
                else
                    work.Add((file, await GetDurationSecondsAsync(file, cancellationToken)));
            }
            result.Skipped = skipped.Count;

            double totalDuration = 0;
            foreach (var w in work)
                totalDuration += w.Duration > 0 ? w.Duration : 0;

            var stopwatch = Stopwatch.StartNew();
            double completedDuration = 0;

            for (int i = 0; i < work.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }

                var (file, duration) = work[i];
                int fileIndex = i + 1;
                double durationBefore = completedDuration;

                void Report(double filePercent, double? speed)
                {
                    double overall = totalDuration > 0
                        ? (durationBefore + (duration > 0 ? duration * filePercent / 100.0 : 0)) / totalDuration * 100.0
                        : (double)i / Math.Max(1, work.Count) * 100.0;
                    overall = Math.Clamp(overall, 0, 100);

                    TimeSpan? eta = null;
                    if (overall > 0.5)
                    {
                        double totalEstMs = stopwatch.Elapsed.TotalMilliseconds / (overall / 100.0);
                        double remainingMs = Math.Max(0, totalEstMs - stopwatch.Elapsed.TotalMilliseconds);
                        eta = TimeSpan.FromMilliseconds(remainingMs);
                    }

                    progress?.Report(new ProxyBatchProgress
                    {
                        CurrentFileIndex = fileIndex,
                        TotalFiles = work.Count,
                        CurrentFileName = Path.GetFileName(file),
                        CurrentFilePercent = Math.Clamp(filePercent, 0, 100),
                        OverallPercent = overall,
                        Elapsed = stopwatch.Elapsed,
                        Eta = eta,
                        Speed = speed
                    });
                }

                Report(0, null);

                bool ok;
                try
                {
                    ok = await GenerateSingleAsync(file, duration, options, Report, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    result.Cancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    ok = false;
                    result.Errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }

                if (ok)
                    result.Succeeded++;
                else
                    result.Failed++;

                completedDuration += duration > 0 ? duration : 0;
                Report(100, null);
            }

            return result;
        }

        /// <summary>
        /// Generates a single proxy. Tries GPU (AMF) first when requested, then falls back to a fast
        /// software encode. Returns true on success.
        /// </summary>
        private async Task<bool> GenerateSingleAsync(
            string sourceFile,
            double durationSeconds,
            ProxyOptions options,
            Action<double, double?> report,
            CancellationToken cancellationToken)
        {
            string proxyPath = ProxyService.GetProxyPath(sourceFile);
            string proxyFolder = ProxyService.GetProxyFolder(sourceFile);
            EnsureProxyFolder(proxyFolder);

            // Prefer GPU encoding for speed; fall back to software if it fails.
            if (options.UseHardwareEncoding)
            {
                bool ok = await RunFFmpegAsync(
                    BuildArgs(sourceFile, proxyPath, options, hardware: true),
                    proxyPath, durationSeconds, report, cancellationToken);
                if (ok)
                    return true;

                Logger.LogDebug($"AMF proxy encode failed for {Path.GetFileName(sourceFile)}, falling back to software.", "Proxy");
            }

            return await RunFFmpegAsync(
                BuildArgs(sourceFile, proxyPath, options, hardware: false),
                proxyPath, durationSeconds, report, cancellationToken);
        }

        private static void EnsureProxyFolder(string proxyFolder)
        {
            if (string.IsNullOrEmpty(proxyFolder))
                return;

            var info = Directory.CreateDirectory(proxyFolder);
            try
            {
                // Keep the proxy folder out of the way in Explorer too.
                if (!info.Attributes.HasFlag(FileAttributes.Hidden))
                    info.Attributes |= FileAttributes.Hidden;
            }
            catch { /* attribute set is best-effort */ }
        }

        private static string BuildArgs(string sourceFile, string proxyPath, ProxyOptions options, bool hardware)
        {
            var args = new List<string>
            {
                "-y",
                "-hwaccel auto",              // accelerate decode of heavy 4K/high-fps sources
                $"-i \"{sourceFile}\"",
                "-map 0:v:0",
                "-map 0:a?",                  // include audio only if present
            };

            // Video filter chain: downscale (preserving aspect, even dimensions) and optional fps cap.
            var filters = new List<string>();
            if (options.Height > 0)
                filters.Add($"scale=-2:{options.Height}");
            if (options.FrameRate > 0)
                filters.Add($"fps={options.FrameRate}");
            if (filters.Count > 0)
                args.Add($"-vf \"{string.Join(",", filters)}\"");

            if (hardware)
            {
                // AMD AMF: bias hard toward speed.
                args.Add("-c:v h264_amf");
                args.Add("-quality speed");
                args.Add("-rc cqp");
                args.Add("-qp_i 26 -qp_p 26 -qp_b 26");
            }
            else
            {
                args.Add("-c:v libx264");
                args.Add("-preset veryfast");
                args.Add("-crf 23");
                args.Add("-pix_fmt yuv420p");
            }

            args.Add("-c:a aac");
            args.Add("-b:a 128k");
            args.Add("-movflags +faststart");
            args.Add($"\"{proxyPath}\"");

            return string.Join(" ", args);
        }

        private async Task<bool> RunFFmpegAsync(
            string arguments,
            string proxyPath,
            double durationSeconds,
            Action<double, double?> report,
            CancellationToken cancellationToken)
        {
            Trace.WriteLine($"Proxy: {Globals.FFmpegPath} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = Globals.FFmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = new Process { StartInfo = startInfo };
            var errorBuffer = new StringBuilder();

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;
                errorBuffer.AppendLine(e.Data);

                var match = TimeRegex.Match(e.Data);
                if (match.Success && durationSeconds > 0 &&
                    TimeSpan.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var t))
                {
                    double percent = Math.Clamp(t.TotalSeconds / durationSeconds * 100.0, 0, 100);
                    double? speed = null;
                    var speedMatch = SpeedRegex.Match(e.Data);
                    if (speedMatch.Success &&
                        double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                        speed = s;
                    report(percent, speed);
                }
            };

            try
            {
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                using var registration = cancellationToken.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(true); }
                    catch { /* ignore */ }
                });

                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryDeletePartial(proxyPath);
                throw;
            }

            bool success = process.ExitCode == 0 && File.Exists(proxyPath) && new FileInfo(proxyPath).Length > 0;
            if (!success)
            {
                TryDeletePartial(proxyPath);
                Trace.WriteLine($"Proxy encode failed (exit {process.ExitCode}): {errorBuffer.ToString().Trim()}");
            }
            return success;
        }

        private static void TryDeletePartial(string proxyPath)
        {
            try
            {
                if (File.Exists(proxyPath))
                    File.Delete(proxyPath);
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Reads a video's duration in seconds via ffprobe. Returns 0 when it can't be determined
        /// (progress then falls back to a per-file-count estimate).
        /// </summary>
        private static async Task<double> GetDurationSecondsAsync(string file, CancellationToken cancellationToken)
        {
            if (!File.Exists(Globals.FFProbePath))
                return 0;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Globals.FFProbePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cancellationToken);

                if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                    return seconds;
            }
            catch { /* fall through to 0 */ }

            return 0;
        }
    }
}
