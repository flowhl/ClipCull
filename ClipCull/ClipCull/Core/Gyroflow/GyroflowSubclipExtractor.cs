using CliWrap;
using ClipCull.Extensions;
using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core.Gyroflow
{
    public class GyroflowSubclipExtractor
    {
        public class SubclipInfo : INotifyPropertyChanged
        {
            private string _videoFile;
            public string VideoFile
            {
                get => _videoFile;
                set
                {
                    _videoFile = value;
                    OnPropertyChanged(nameof(VideoFile));
                }
            }
            private TimeSpan _startTime;
            public TimeSpan StartTime
            {
                get => _startTime;
                set
                {
                    _startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                    OnPropertyChanged(nameof(DurationString));
                }
            }
            private TimeSpan _endTime;
            public TimeSpan EndTime
            {
                get => _endTime;
                set
                {
                    _endTime = value;
                    OnPropertyChanged(nameof(EndTime));
                    OnPropertyChanged(nameof(DurationString));
                }
            }

            private string _outputName;
            public string OutputName
            {
                get => _outputName;
                set
                {
                    _outputName = value;
                    OnPropertyChanged(nameof(OutputName));
                }
            }

            private bool _rendering;
            public bool Rendering
            {
                get => _rendering;
                set
                {
                    _rendering = value;
                    OnPropertyChanged(nameof(Rendering));
                }
            }

            private bool _rendered;
            public bool Rendered
            {
                get => _rendered;
                set
                {
                    _rendered = value;
                    OnPropertyChanged(nameof(Rendered));
                }
            }

            public string DurationString
            {
                get
                {
                    return "Duration: " +
                        (EndTime - StartTime).TotalSeconds.ToString("0.00") + " seconds";
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
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
            if (subclip.OutputName.IsNullOrEmpty())
            {
                throw new ArgumentException("Output name for subclip cannot be null or empty.");
            }
            var outputFile = Path.Combine(_outputDirectory, subclip.OutputName);

            // Build Gyroflow CLI arguments
            var args = BuildGyroflowArgs(subclip, outputFile, overwrite, parallelRenders);

            //Sanitize args
            args = args.Replace("\\", "/");

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

            // Build output parameters object manually with single quotes and double braces
            var paramParts = new List<string>();
            paramParts.Add($"'output_folder': '{Path.GetDirectoryName(outputFile)}'");
            paramParts.Add($"'output_filename': '{Path.GetFileName(outputFile)}'");
            paramParts.Add("'use_gpu': true");
            paramParts.Add("'audio': true");

            // Add trim settings if we have time ranges
            if (subclip.StartTime != TimeSpan.Zero || subclip.EndTime != TimeSpan.Zero)
            {
                paramParts.Add($"'trim_start': {subclip.StartTime.TotalSeconds}");
                if (subclip.EndTime != TimeSpan.Zero)
                {
                    paramParts.Add($"'trim_end': {subclip.EndTime.TotalSeconds}");
                }
            }

            // Format JSON with single quotes and double braces like their documentation
            string jsonParams = "\"{ " + string.Join(", ", paramParts) + " }\"";
            args.Add("--out-params");
            args.Add($"{jsonParams}");

            //Suffix
            string outputFileName = Path.GetFileName(outputFile);
            string inputFileName = Path.GetFileName(subclip.VideoFile);
            string suffix = outputFileName.StartsWith(inputFileName)
                ? outputFileName.Substring(inputFileName.Length)
                : "_" + outputFileName;

            return string.Join(" ", args);
        }

        private async Task RunGyroflowCli(string arguments)
        {
            Trace.WriteLine($"Running: {_gyroflowExePath} {arguments}");

            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _gyroflowExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = new Process { StartInfo = processStartInfo };

            // Handle output data
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdOutBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"Output: {e.Data}");
                }
            };

            // Handle error data
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || stdErrBuffer.ToString().Trim().Length > 0)
            {
                string error = stdErrBuffer.ToString().Trim();
                string msg = $"Gyroflow CLI failed with exit code {process.ExitCode}; Error: {error}";
                throw new Exception(msg);
            }
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
}