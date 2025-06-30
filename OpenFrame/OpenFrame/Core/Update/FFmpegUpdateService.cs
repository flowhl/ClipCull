using OpenFrame.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Core.Update
{
    public class FFmpegUpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string[] _requiredExecutables = { "ffmpeg.exe", "ffplay.exe", "ffprobe.exe" };

        public FFmpegUpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FFmpegUpdateService/1.0");
        }

        /// <summary>
        /// Checks for missing FFmpeg executables and downloads/installs them if needed
        /// </summary>
        /// <param name="customPath">Target directory for FFmpeg executables</param>
        /// <returns>True if any files were updated, false if all were already present</returns>
        public async Task<bool> EnsureFFmpegAsync(string customPath)
        {
            try
            {
                // Check if any executables are missing
                var missingExecutables = GetMissingExecutables(customPath);

                if (!missingExecutables.Any())
                {
                    Trace.WriteLine("All FFmpeg executables are already present.");
                    return false;
                }

                Trace.WriteLine($"Missing executables: {string.Join(", ", missingExecutables)}");
                Trace.WriteLine("Downloading and installing FFmpeg...");

                await DownloadAndInstallFFmpegAsync(customPath);

                // Verify installation
                var stillMissing = GetMissingExecutables(customPath);
                if (stillMissing.Any())
                {
                    throw new InvalidOperationException($"Installation failed. Still missing: {string.Join(", ", stillMissing)}");
                }

                Trace.WriteLine("FFmpeg installation completed successfully.");
                Logger.LogInfo($"FFmpeg updated successfully. Installed: {string.Join(", ", _requiredExecutables)}", "FFmpeg update completed");
                return true;
            }
            catch (Exception ex)
            {
                string msg = $"Failed to update FFmpeg: {ex.GetFullDetails()}";
                Trace.WriteLine(msg);
                Logger.LogError(msg, ex, "FFmpeg update failed");
                throw;
            }
        }

        /// <summary>
        /// Forces a fresh download and installation regardless of existing files
        /// </summary>
        /// <param name="customPath">Target directory for FFmpeg executables</param>
        public async Task ForceUpdateFFmpegAsync(string customPath)
        {
            Trace.WriteLine("Force updating FFmpeg...");
            await DownloadAndInstallFFmpegAsync(customPath);
            Trace.WriteLine("Force update completed.");
        }

        private string[] GetMissingExecutables(string customPath)
        {
            if (!Directory.Exists(customPath))
            {
                Directory.CreateDirectory(customPath);
                return _requiredExecutables; // All missing if directory doesn't exist
            }

            return _requiredExecutables
                .Where(exe => !File.Exists(Path.Combine(customPath, exe)))
                .ToArray();
        }

        private async Task DownloadAndInstallFFmpegAsync(string customPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"ffmpeg_temp_{Guid.NewGuid():N}");
            string zipPath = Path.Combine(tempDir, "ffmpeg.zip");

            try
            {
                // Create temp directory
                Directory.CreateDirectory(tempDir);

                // Download FFmpeg zip
                const string downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
                Trace.WriteLine($"Downloading from: {downloadUrl}");

                using (var response = await _httpClient.GetAsync(downloadUrl))
                {
                    response.EnsureSuccessStatusCode();

                    using (var fileStream = File.Create(zipPath))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }

                Trace.WriteLine($"Downloaded {new FileInfo(zipPath).Length / 1024 / 1024:F1} MB");

                // Extract zip
                Trace.WriteLine("Extracting archive...");
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var binEntries = archive.Entries
                        .Where(entry => entry.FullName.Contains("/bin/") &&
                                       _requiredExecutables.Any(exe => entry.Name.Equals(exe, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (!binEntries.Any())
                    {
                        throw new InvalidOperationException("No required executables found in the downloaded archive.");
                    }

                    // Ensure target directory exists
                    Directory.CreateDirectory(customPath);

                    // Extract each required executable
                    foreach (var entry in binEntries)
                    {
                        string targetPath = Path.Combine(customPath, entry.Name);
                        Trace.WriteLine($"Extracting {entry.Name}...");

                        // Delete existing file if it exists
                        if (File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                        }

                        entry.ExtractToFile(targetPath, overwrite: true);

                        // Verify extraction
                        if (!File.Exists(targetPath))
                        {
                            throw new InvalidOperationException($"Failed to extract {entry.Name}");
                        }

                        Trace.WriteLine($"✓ {entry.Name} installed ({new FileInfo(targetPath).Length / 1024 / 1024:F1} MB)");
                    }
                }
            }
            finally
            {
                // Cleanup temp directory
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                        Trace.WriteLine("Cleanup completed.");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Warning: Failed to cleanup temp directory {tempDir}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets version info for installed FFmpeg (if available)
        /// </summary>
        /// <param name="customPath">Directory containing FFmpeg executables</param>
        /// <returns>Version string or null if not available</returns>
        public async Task<string> GetFFmpegVersionAsync(string customPath)
        {
            string ffmpegPath = Path.Combine(customPath, "ffmpeg.exe");

            if (!File.Exists(ffmpegPath))
            {
                return null;
            }

            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Extract version from first line
                var firstLine = output.Split('\n').FirstOrDefault()?.Trim();
                return firstLine?.StartsWith("ffmpeg version") == true ? firstLine : "Unknown version";
            }
            catch
            {
                return "Version check failed";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
