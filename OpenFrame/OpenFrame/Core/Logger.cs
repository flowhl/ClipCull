using Notifications.Wpf;
using Notifications.Wpf.Controls;
using OpenFrame.Extensions;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace OpenFrame.Core
{
    public static class Logger
    {
        private static NotificationManager _notificationManager = new NotificationManager();
        private static ILogger _logger;
        private static LogEventLevel _currentLogLevel = LogEventLevel.Information;

        static Logger()
        {
            InitializeLogger();
        }

        /// <summary>
        /// Initialize Serilog logger with file rotation and configurable verbosity
        /// </summary>
        private static void InitializeLogger()
        {
            try
            {
                // Ensure log directory exists
                if (!Directory.Exists(Globals.LogPath))
                {
                    Directory.CreateDirectory(Globals.LogPath);
                }

                var logFilePath = Path.Combine(Globals.LogPath, "application-.log");

                _logger = new LoggerConfiguration()
                    .MinimumLevel.Is(_currentLogLevel)
                    .WriteTo.File(
                        path: logFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30, // Keep 30 days of logs
                        fileSizeLimitBytes: 50 * 1024 * 1024, // 50MB per file
                        rollOnFileSizeLimit: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )
                    .WriteTo.Console(
                        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )
                    .CreateLogger();

                // Set Serilog as the global logger
                Log.Logger = _logger;
            }
            catch (Exception ex)
            {
                // Fallback notification if logger initialization fails
                _notificationManager.Show(new NotificationContent
                {
                    Title = "Logger Error",
                    Message = $"Failed to initialize logger: {ex.Message}",
                    Type = NotificationType.Error
                });
            }
        }

        /// <summary>
        /// Set log verbosity level (0=Verbose, 1=Debug, 2=Information, 3=Warning, 4=Error, 5=Fatal)
        /// </summary>
        /// <param name="verbosityLevel">Integer verbosity level</param>
        public static void SetVerbosity(int verbosityLevel)
        {
            _currentLogLevel = verbosityLevel switch
            {
                0 => LogEventLevel.Verbose,
                1 => LogEventLevel.Debug,
                2 => LogEventLevel.Information,
                3 => LogEventLevel.Warning,
                4 => LogEventLevel.Error,
                5 => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };

            // Recreate logger with new minimum level
            Log.CloseAndFlush();
            InitializeLogger();
        }

        /// <summary>
        /// Get current verbosity level as integer
        /// </summary>
        public static int GetVerbosity()
        {
            return _currentLogLevel switch
            {
                LogEventLevel.Verbose => 0,
                LogEventLevel.Debug => 1,
                LogEventLevel.Information => 2,
                LogEventLevel.Warning => 3,
                LogEventLevel.Error => 4,
                LogEventLevel.Fatal => 5,
                _ => 2
            };
        }

        public static void LogVerbose(string message, string title = null)
        {
            _logger?.Verbose(message);
            // Verbose logs typically don't show notifications
        }

        public static void LogDebug(string message, string title = null)
        {
            _logger?.Debug(message);
            // Debug logs typically don't show notifications unless explicitly needed
        }

        public static void LogInfo(string message, string title = null)
        {
            _logger?.Information(message);

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Information",
                Message = message,
                Type = NotificationType.Information
            });
        }

        public static void LogWarning(string message, string title = null)
        {
            _logger?.Warning(message);

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Warning",
                Message = message,
                Type = NotificationType.Warning
            });
        }

        public static void LogError(string message, string title = null)
        {
            _logger?.Error(message);

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Error",
                Message = message,
                Type = NotificationType.Error
            });
        }

        public static void LogError(string message, Exception ex, string title = null)
        {
            _logger?.Error(ex, message);

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Error",
                Message = $"{message}\n\n{ex.GetFullDetails()}",
                Type = NotificationType.Error
            });
        }

        public static void LogError(Exception ex, string title = null)
        {
            _logger?.Error(ex, "An error occurred");

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Error",
                Message = ex.GetFullDetails(),
                Type = NotificationType.Error
            });
        }

        public static void LogFatal(string message, string title = null)
        {
            _logger?.Fatal(message);

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Fatal Error",
                Message = message,
                Type = NotificationType.Error
            });
        }

        public static void LogFatal(Exception ex, string message = null, string title = null)
        {
            _logger?.Fatal(ex, message ?? "A fatal error occurred");

            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Fatal Error",
                Message = $"{message ?? "A fatal error occurred"}\n\n{ex.GetFullDetails()}",
                Type = NotificationType.Error
            });
        }

        /// <summary>
        /// Close and flush the logger
        /// </summary>
        public static void Dispose()
        {
            Log.CloseAndFlush();
        }
    }
}