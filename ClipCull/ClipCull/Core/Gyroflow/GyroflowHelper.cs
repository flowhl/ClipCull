using CliWrap;
using ClipCull.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core.Gyroflow
{
    public static class GyroflowHelper
    {
        public static string GetGyroflowInstallationPath()
        {
            string dir = GetGyroflowInstallationDir();
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
            string userProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!Directory.Exists(userProgramFiles))
            {
                Trace.WriteLine("GyroflowHelper: Program Files directory does not exist, trying Program Files (x86).");
                userProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            }
            if (!Directory.Exists(userProgramFiles))
            {
                Trace.WriteLine("GyroflowHelper: Program Files (x86) directory does not exist.");
                return null;
            }
            string windowsApps = Path.Combine(userProgramFiles, "WindowsApps");
            if (!Directory.Exists(windowsApps))
            {
                Trace.WriteLine("GyroflowHelper: WindowsApps directory does not exist.");
                return null;
            }
            var directories = Directory.GetDirectories(windowsApps, "*Gyroflow*", SearchOption.TopDirectoryOnly);
            if (directories.Length == 0)
            {
                Trace.WriteLine("GyroflowHelper: No Gyroflow installation found in WindowsApps.");
                return null;
            }
            if (directories.Length > 1)
            {
                Trace.WriteLine("GyroflowHelper: Multiple Gyroflow installations found, using the first one.");
                var versions = directories
                    .Where(d => d.IsNotNullOrEmpty())
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.Name)
                    .Select(d => d.FullName);

                foreach (var version in versions)
                {
                    string[] gyroflowPaths = {
                        Path.Combine(version, "gyroflow.exe"),
                        Path.Combine(version, "Gyroflow.exe"),
                        Path.Combine(version, "GyroFlow.exe")
                    };
                    if (gyroflowPaths.Any(File.Exists))
                    {
                        Trace.WriteLine($"GyroflowHelper: Found Gyroflow executable at {version}.");
                        return version;
                    }
                }
            }
            var firstDirectory = directories.FirstOrDefault();
            if (firstDirectory != null)
            {
                Trace.WriteLine($"GyroflowHelper: Using Gyroflow installation at {firstDirectory}.");
                return firstDirectory;
            }
            Trace.WriteLine("GyroflowHelper: No valid Gyroflow installation found.");

            string gyroflowPath = Environment.GetEnvironmentVariable("GYROFLOW_PATH");
            if (gyroflowPath.IsNotNullOrEmpty())
            {
                Trace.WriteLine($"GyroflowHelper: Using Gyroflow installation from environment variable GYROFLOW_PATH: {gyroflowPath}.");
                return gyroflowPath;
            }
            //try "where gyroflow" in cli using cliwrap
            var stdOutBuffer = new StringBuilder();
            CliWrap.Cli.Wrap("where")
                .WithArguments("gyroflow")
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();
            if (stdOutBuffer.Length > 0)
            {
                string path = stdOutBuffer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (File.Exists(path))
                {
                    Trace.WriteLine($"GyroflowHelper: Using Gyroflow installation from CLI: {path}.");
                    return Path.GetDirectoryName(path);
                }
            }
            return null;
        }
    }
}