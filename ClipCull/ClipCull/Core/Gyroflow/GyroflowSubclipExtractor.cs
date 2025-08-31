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
using System.Text.RegularExpressions;

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

            private string _status;
            public string Status
            {
                get => _status;
                set
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
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

            public int Rotation { get; set; }

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

        public async Task<List<string>> ExtractSubclips(List<SubclipInfo> subclips, Action<string> progressCallback, bool overwrite = false, int parallelRenders = 1)
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
                    string outputFile = await ExtractSubclip(subclip, overwrite, parallelRenders, progressCallback);
                    outputFiles.Add(outputFile);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to extract subclip using Gyroflow", ex, "Gyroflow Render Error");
                }
            }
            return outputFiles;
        }

        public async Task<string> ExtractSubclip(SubclipInfo subclip, bool overwrite, int parallelRenders, Action<string> progressCallback)
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
            await RunGyroflowCli(args, outputFile, progressCallback);
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

            string useAudio = SettingsHandler.Settings.GyroflowDisableAudio ? "false" : "true";
            // Build output parameters object manually with single quotes and double braces
            var paramParts = new List<string>();
            paramParts.Add($"'output_folder': '{Path.GetDirectoryName(outputFile)}'");
            paramParts.Add($"'output_filename': '{Path.GetFileName(outputFile)}'");
            paramParts.Add("'use_gpu': true");
            paramParts.Add($"'audio': {useAudio}");
            if (!SettingsHandler.Settings.GyroflowDisableAudio && SettingsHandler.Settings.GyroflowUseOtherAudioCodec)
            {
                //specify PCM (s16le) codec
                paramParts.Add("'audio_codec': 'PCM (s16le)'");
            }

            // Format JSON with single quotes and double braces like their documentation
            string jsonParams = "\"{ " + string.Join(", ", paramParts) + " }\"";
            args.Add("--out-params");
            args.Add($"{jsonParams}");

            //--preset "{'video_info': {'rotation': 90}}"

            string trimInfo = null;
            if (subclip.StartTime != TimeSpan.Zero || subclip.EndTime != TimeSpan.Zero)
            {

                double startTimeMs = subclip.StartTime.TotalMilliseconds;
                double endTimeMs = subclip.EndTime.TotalMilliseconds;

                trimInfo = "'trim_ranges_ms': [[" + startTimeMs + ", " + endTimeMs + "]],";
            }

            var metaData = VideoMetadataReader.ReadMetadata(subclip.VideoFile);
            var height = metaData?.Height;
            var width = metaData?.Width;

            string rotationInfo = " 'video_info': {'rotation': " + subclip.Rotation + "}";

            // Build the preset JSON with single backslash escaping
            string presetJson = "{'version': 2, " + trimInfo + rotationInfo;

            // If height and width are available, add output dimensions
            if (height.HasValue && width.HasValue)
            {
                int realHeight = height.Value;
                int realWidth = width.Value;
                if (subclip.Rotation == 90 || subclip.Rotation == 270)
                {
                    int temp = realHeight;
                    realHeight = realWidth;
                    realWidth = temp;
                }
                presetJson += ", 'output': {'output_width': " + realWidth + ", 'output_height': " + realHeight + "}";
            }

            presetJson += "}";

            // Add the complete preset argument
            args.Add($"--preset \"{presetJson}\"");

            //Print progress to stdout for parsing
            args.Add($"--stdout-progress");

            //Suffix
            string outputFileName = Path.GetFileName(outputFile);
            string inputFileName = Path.GetFileName(subclip.VideoFile);
            string suffix = outputFileName.StartsWith(inputFileName)
                ? outputFileName.Substring(inputFileName.Length)
                : "_" + outputFileName;

            return string.Join(" ", args);
        }

        private async Task RunGyroflowCli(string arguments, string outputFile, Action<string> progressCallback)
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
                WindowStyle = ProcessWindowStyle.Hidden,

                // Force non-TTY behavior
                Environment = { ["TERM"] = "dumb" }
            };

            using var process = new Process { StartInfo = processStartInfo };

            // Handle error data (keep this as events work fine for stderr)
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"Error: {e.Data}");
                }
            };
            // Handle output data
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    progressCallback(e.Data);
                    stdOutBuffer.AppendLine(e.Data);
                    Trace.WriteLine($"Output: {e.Data}");
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            //Track progress bar
            //var buffer = new char[1024];
            //using var reader = process.StandardOutput;

            //var progressTrackingTask = Task.Run(async () =>
            //{
            //    while (!process.HasExited)
            //    {
            //        var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
            //        if (charsRead > 0)
            //        {
            //            var output = new string(buffer, 0, charsRead);

            //            string progressInfo = ParseProgress(output);
            //            Application.Current.Dispatcher.Invoke(() =>
            //            {
            //                progressCallback(progressInfo);
            //            });
            //        }
            //        await Task.Delay(100);
            //    }
            //});

            await process.WaitForExitAsync();
            //await progressTrackingTask;

            if (process.ExitCode != 0 || stdErrBuffer.ToString().Trim().Length > 0)
            {
                string error = stdErrBuffer.ToString().Trim();
                string msg = $"Gyroflow CLI failed with exit code {process.ExitCode}; Error: {error}";
                if (error.Contains("Number of bands", StringComparison.OrdinalIgnoreCase) && error.Contains("exceeds limit", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("The issue seems to be related to audio encoding, try switching to a different codec in the settings or disable audio.");
                }
                if (File.Exists(outputFile))
                {
                    Logger.LogDebug("File created anyways, not throwing an exception");
                    return;
                }
                throw new Exception(msg);
            }
        }

        private string ParseProgress(string output)
        {
            if (string.IsNullOrEmpty(output))
                return null;

            // Split by lines and carriage returns to handle progress bar updates
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Look for lines that contain progress indicators
                if (line.Contains("Elapsed:") && line.Contains("/") && line.Contains("ETA"))
                {
                    // Use regex to extract the three parts we want
                    var match = Regex.Match(line,
                        @"Elapsed:\s*(\d{2}:\d{2}:\d{2}).*?(\d+/\d+)\s+ETA\s*([0-9.]+s)",
                        RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        var elapsed = match.Groups[1].Value;
                        var progress = match.Groups[2].Value;
                        var eta = match.Groups[3].Value;

                        return $"Elapsed: {elapsed} {progress} ETA {eta}";
                    }
                }
            }

            return null;
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