using CliWrap;
using ClipCull.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ClipCull.Core.Gyroflow
{
    public static class GyroflowHelper
    {
        public static string GetGyroflowInstallationPath()
        {
            string dir = null;
            try
            {
                dir = GetGyroflowInstallationDir();
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"The programm does not have permission to access the Gyroflow installation directory. Please run ClipCull as administator to fix the issue or move the gyroflow installation to another folder. {ex.Message}", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError($"Access to Gyroflow installation directory denied: {ex.Message}", "Gyroflow installation error");
            }

            if (dir == null)
            {
                Trace.WriteLine("GyroflowHelper: Gyroflow installation directory is null, returning null.");
                return null;
            }

            Trace.WriteLine($"GyroflowHelper: Gyroflow installation directory: {dir}");
            if (!Directory.Exists(dir))
            {
                Trace.WriteLine("GyroflowHelper: Gyroflow installation directory does not exist, returning null.");
                return null;
            }
            string gyroflowExe = Path.Combine(dir, "gyroflow.exe");
            if (!File.Exists(gyroflowExe))
            {
                Trace.WriteLine("GyroflowHelper: 'gyroflow.exe' executable does not exist.");
                gyroflowExe = Path.Combine(dir, "Gyroflow.exe");
            }
            if (!File.Exists(gyroflowExe))
            {
                Trace.WriteLine("GyroflowHelper: 'Gyroflow.exe' executable does not exist.");
                gyroflowExe = Path.Combine(dir, "GyroFlow.exe");
            }
            if (!File.Exists(gyroflowExe))
            {
                Trace.WriteLine("GyroflowHelper: 'GyroFlow.exe' executable does not exist.");
                return null;
            }
            return gyroflowExe;

        }
        /// <summary>
        /// Gets the installation path of Gyroflow from the WindowsApps directory.
        /// Tries different ways as a fallback, returns the path of the directory where gyroflow.exe lives
        /// </summary>
        /// <returns></returns>
        public static string GetGyroflowInstallationDir()
        {
            // First try to find via PowerShell (bypasses WindowsApps permissions)
            string gyroflowDir = FindGyroflowViaAppxPackage();
            if (gyroflowDir != null)
            {
                Trace.WriteLine($"GyroflowHelper: Found Gyroflow via AppxPackage at {gyroflowDir}.");
                return gyroflowDir;
            }

            // Try environment variable
            string gyroflowPath = Environment.GetEnvironmentVariable("GYROFLOW_PATH");
            if (!string.IsNullOrEmpty(gyroflowPath) && Directory.Exists(gyroflowPath))
            {
                Trace.WriteLine($"GyroflowHelper: Using Gyroflow installation from environment variable GYROFLOW_PATH: {gyroflowPath}.");
                return gyroflowPath;
            }

            // Try "where gyroflow" in CLI
            gyroflowDir = FindGyroflowViaWhereCommand();
            if (gyroflowDir != null)
            {
                Trace.WriteLine($"GyroflowHelper: Found Gyroflow via CLI at {gyroflowDir}.");
                return gyroflowDir;
            }

            // Try PowerShell directory search as last resort
            gyroflowDir = FindGyroflowViaPowerShellSearch();
            if (gyroflowDir != null)
            {
                Trace.WriteLine($"GyroflowHelper: Found Gyroflow via PowerShell search at {gyroflowDir}.");
                return gyroflowDir;
            }

            Trace.WriteLine("GyroflowHelper: No valid Gyroflow installation found.");
            return null;
        }

        private static string FindGyroflowViaAppxPackage()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-AppxPackage | Where-Object {$_.Name -like '*Gyroflow*'} | Select-Object -First 1 -ExpandProperty InstallLocation\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
                    {
                        // Verify the executable exists
                        string[] possibleExes = {
                    Path.Combine(output, "gyroflow.exe"),
                    Path.Combine(output, "Gyroflow.exe"),
                    Path.Combine(output, "GyroFlow.exe")
                };

                        if (possibleExes.Any(File.Exists))
                        {
                            return output;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"GyroflowHelper: Error finding Gyroflow via AppxPackage: {ex.Message}");
            }

            return null;
        }

        private static string FindGyroflowViaWhereCommand()
        {
            try
            {
                var stdOutBuffer = new StringBuilder();
                var stdErrBuffer = new StringBuilder();

                var result = CliWrap.Cli.Wrap("where")
                    .WithArguments("gyroflow")
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode == 0 && stdOutBuffer.Length > 0)
                {
                    string path = stdOutBuffer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return Path.GetDirectoryName(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"GyroflowHelper: Error finding Gyroflow via WHERE command: {ex.Message}");
            }

            return null;
        }

        private static string FindGyroflowViaPowerShellSearch()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-ChildItem 'C:\\Program Files\\WindowsApps' -Directory -ErrorAction SilentlyContinue | Where-Object {$_.Name -like '*Gyroflow*'} | Select-Object -First 1 -ExpandProperty FullName\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
                    {
                        // Verify the executable exists
                        string[] possibleExes = {
                    Path.Combine(output, "gyroflow.exe"),
                    Path.Combine(output, "Gyroflow.exe"),
                    Path.Combine(output, "GyroFlow.exe")
                };

                        if (possibleExes.Any(File.Exists))
                        {
                            return output;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"GyroflowHelper: Error finding Gyroflow via PowerShell search: {ex.Message}");
            }

            return null;
        }
    }
}