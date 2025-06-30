using OpenFrame.Extensions;
using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Core.Gyroflow
{
    public class GyroflowSubclipExtractor
    {
        public class SubclipInfo
        {
            public string VideoFile { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string OutputName { get; set; }
        }

        private readonly string _gyroflowExePath;
        private readonly string _outputDirectory;
        private readonly string _settingsFile;

        public GyroflowSubclipExtractor(string outputDirectory, string settingsFile = null)
        {
            _gyroflowExePath = GetGyroflowInstallationPath();
            _outputDirectory = outputDirectory;
            _settingsFile = settingsFile; // Optional .gyroflow file for custom settings

            Directory.CreateDirectory(_outputDirectory);
        }

        public async Task<List<string>> ExtractSubclips(List<SubclipInfo> subclips, bool overwrite = false, int parallelRenders = 1)
        {
            if (string.IsNullOrEmpty(_gyroflowExePath) || !File.Exists(_gyroflowExePath))
            {
                throw new FileNotFoundException("Gyroflow executable not found. Please ensure Gyroflow is installed and the path is set correctly in settings.");
            }

            var outputFiles = new List<string>();

            foreach (var subclip in subclips)
            {
                try
                {
                    string outputFile = await ExtractSubclip(subclip, overwrite, parallelRenders);
                    outputFiles.Add(outputFile);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to extract subclip using Gyroflow", ex, "Gyroflow Render Error");
                }
            }
            return outputFiles;
        }

        public async Task<string> ExtractSubclip(SubclipInfo subclip, bool overwrite, int parallelRenders)
        {
            if (string.IsNullOrEmpty(_gyroflowExePath) || !File.Exists(_gyroflowExePath))
            {
                throw new FileNotFoundException("Gyroflow executable not found. Please ensure Gyroflow is installed and the path is set correctly in settings.");
            }

            Trace.WriteLine($"Processing: {subclip.VideoFile} ({subclip.StartTime} - {subclip.EndTime})");

            // Generate output filename
            var outputFile = Path.Combine(_outputDirectory,
            string.IsNullOrEmpty(subclip.OutputName)
            ? $"{Path.GetFileNameWithoutExtension(subclip.VideoFile)}_subclip_{subclip.StartTime:mm\\-ss}_{subclip.EndTime:mm\\-ss}_stabilized.mp4"
            : subclip.OutputName);

            // Build Gyroflow CLI arguments
            var args = BuildGyroflowArgs(subclip, outputFile, overwrite, parallelRenders);

            // Run Gyroflow CLI
            await RunGyroflowCli(args);
            return outputFile;
        }

        private string BuildGyroflowArgs(SubclipInfo subclip, string outputFile, bool overwrite, int parallelRenders)
        {
            var args = new List<string>();

            // Input files - video file first, then optional settings file
            args.Add($"\"{subclip.VideoFile}\"");

            if (!string.IsNullOrEmpty(_settingsFile) && File.Exists(_settingsFile))
            {
                args.Add($"\"{_settingsFile}\"");
            }

            // Parallel renders
            if (parallelRenders > 1)
            {
                args.Add($"-j {parallelRenders}");
            }

            // Overwrite flag
            if (overwrite)
            {
                args.Add("-f");
            }

            // Output parameters - specify output path and trim settings
            var outputParams = new List<string>
        {
            $"'output_path': '{outputFile.Replace("\\", "\\\\").Replace("'", "\\'")}'",
            "'use_gpu': true",
            "'audio': true"
        };

            // Add trim settings if we have time ranges
            if (subclip.StartTime != TimeSpan.Zero || subclip.EndTime != TimeSpan.Zero)
            {
                outputParams.Add($"'trim_start': {subclip.StartTime.TotalSeconds}");
                if (subclip.EndTime != TimeSpan.Zero)
                {
                    outputParams.Add($"'trim_end': {subclip.EndTime.TotalSeconds}");
                }
            }

            args.Add("-p");
            args.Add($"\"{{ {string.Join(", ", outputParams)} }}\"");

            return string.Join(" ", args);
        }

        private async Task RunGyroflowCli(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _gyroflowExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Trace.WriteLine($"Running: {_gyroflowExePath} {arguments}");

            using var process = Process.Start(startInfo);

            // Read output asynchronously to show progress
            var outputTask = ReadOutputAsync(process.StandardOutput);
            var errorTask = ReadOutputAsync(process.StandardError);

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                Trace.WriteLine($"Gyroflow CLI failed with exit code {process.ExitCode}");
                if (!string.IsNullOrEmpty(error))
                    Trace.WriteLine($"Error: {error}");
                throw new Exception($"Gyroflow CLI failed with exit code {process.ExitCode}");
            }
        }

        private async Task<string> ReadOutputAsync(StreamReader reader)
        {
            var output = "";
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                Trace.WriteLine(line); // Show real-time output
                output += line + Environment.NewLine;
            }
            return output;
        }

        private string GetGyroflowInstallationPath()
        {
            string settingsPath = SettingsHandler.Settings.GyroflowPath;
            if (!settingsPath.IsNullOrEmpty() && File.Exists(settingsPath))
            {
                return settingsPath;
            }
            Trace.WriteLine("Settings for Gyroflow installation path not set or invalid, using default.");
            string defaultPath = GyroflowHelper.GetGyroflowInstallationPath();
            if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
            {
                Trace.WriteLine($"Using Gyroflow installation path: {_gyroflowExePath}");
                return defaultPath;
            }
            return null;
        }
    }

    // Usage example
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Option 1: Use default settings (no .gyroflow file)
            var extractorDefault = new GyroflowSubclipExtractor(
                outputDirectory: @"D:\Output\Stabilized\"
            );

            // Option 2: Use custom settings file
            var extractorCustom = new GyroflowSubclipExtractor(
                outputDirectory: @"D:\Output\Stabilized\",
                settingsFile: @"D:\Settings\my_settings.gyroflow"
            );

            var subclips = new List<GyroflowSubclipExtractor.SubclipInfo>
        {
            new GyroflowSubclipExtractor.SubclipInfo
            {
                VideoFile = @"D:\Videos\FileA.mp4",
                StartTime = TimeSpan.FromSeconds(20),
                EndTime = TimeSpan.FromSeconds(45),
                OutputName = "FileA_clip1.mp4"
            },
            new GyroflowSubclipExtractor.SubclipInfo
            {
                VideoFile = @"D:\Videos\FileB.mp4",
                StartTime = TimeSpan.FromSeconds(38),
                EndTime = TimeSpan.FromSeconds(83), // 1:23
                OutputName = "FileB_clip1.mp4"
            },
            new GyroflowSubclipExtractor.SubclipInfo
            {
                VideoFile = @"D:\Videos\FileB.mp4",
                StartTime = TimeSpan.FromSeconds(75), // 1:15
                EndTime = TimeSpan.FromSeconds(157), // 2:37
                OutputName = "FileB_clip2.mp4"
            }
        };

            try
            {
                // Use custom settings extractor
                var outputFiles = await extractorCustom.ExtractSubclips(
                    subclips,
                    overwrite: true,
                    parallelRenders: 2
                );

                Trace.WriteLine("Extraction completed successfully!");
                Trace.WriteLine("Output files:");
                foreach (var file in outputFiles)
                {
                    Trace.WriteLine($"  - {file}");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}