using Notifications.Wpf;
using Notifications.Wpf.Controls;
using OpenFrame.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Core
{

    public static class Logger
    {
        private static NotificationManager _notificationManager = new NotificationManager();

        public static void LogInfo(string message, string title = null)
        {
            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Information",
                Message = message,
                Type = NotificationType.Information
            });
        }
        public static void LogDebug(string message) { }
        public static void LogWarning(string message, string title = null)
        {
            _notificationManager.Show(new NotificationContent
            {
                Title = "Warning",
                Message = message,
                Type = NotificationType.Warning
            });
        }
        public static void LogError(string message, string title = null)
        {
            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Error",
                Message = message,
                Type = NotificationType.Error
            });
        }
        public static void LogError(string message, Exception ex, string title = null)
        {
            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Error",
                Message = $"{message}\n\n{ex.GetFullDetails()}",
                Type = NotificationType.Error
            });
        }
        public static void LogError(Exception ex, string title = null)
        {
            _notificationManager.Show(new NotificationContent
            {
                Title = title ?? "Error",
                Message = ex.GetFullDetails(),
                Type = NotificationType.Error
            });
        }
    }
}
