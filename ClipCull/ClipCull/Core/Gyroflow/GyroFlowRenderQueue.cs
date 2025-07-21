using ClipCull.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static ClipCull.Core.Gyroflow.GyroflowSubclipExtractor;

namespace ClipCull.Core.Gyroflow
{
    public static class GyroFlowRenderQueue
    {
        public static ObservableCollection<SubclipInfo> Subclips = new ObservableCollection<SubclipInfo>();

        public static async Task RenderAllItemsInQueue()
        {
            if (Subclips.Count == 0)
            {
                Logger.LogWarning("Render queue is empty.", "GyroFlow Render Queue");
                return;
            }

            if (Subclips.Any(x => x.OutputName.IsNullOrEmpty()))
            {
                Logger.LogWarning("Some subclips do not have an output name set.", "GyroFlow Render Queue");
                return;
            }

            string outputDirectory = DialogHelper.ChooseFolder("Select Output Directory for Render");
            if (outputDirectory.IsNullOrEmpty() || !Directory.Exists(outputDirectory))
            {
                return;
            }

            //Check if any of the files should be overwritten
            List<string> existingFiles = Subclips
                .Select(x => Path.Combine(outputDirectory, x.OutputName))
                .Where(x => File.Exists(x))
                .ToList();

            bool overwrite = false;

            if (existingFiles.Count > 0)
            {
                var result = MessageBox.Show(
                    "Some output files already exist. Do you want to overwrite them?",
                    "Overwrite Existing Files",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.Cancel)
                {
                    Logger.LogWarning("Render cancelled by user.", "GyroFlow Render Queue");
                    return;
                }
                overwrite = result == System.Windows.MessageBoxResult.Yes;
            }

            string settingsFile = SettingsHandler.Settings.GyroflowSettingsPath;
            var _renderer = new GyroflowSubclipExtractor(outputDirectory, settingsFile);

            await Task.Run(async Task () =>
            {
                var itemsToDequeue = new List<SubclipInfo>();
                foreach (var item in Subclips)
                {
                    try
                    {
                        item.Rendered = false;
                        item.Rendering = true;
                        string output = await _renderer.ExtractSubclip(item, overwrite, 1);
                        item.Rendering = false;
                        item.Rendered = true;
                        // Remove completed item from queue (on UI thread)
                        itemsToDequeue.Add(item);
                        Logger.LogDebug($"Rendered subclip: {item.VideoFile} from {item.StartTime} to {item.EndTime} -> {output}", "GyroFlow Render Queue");
                    }
                    catch (Exception ex)
                    {
                        item.Rendering = false;
                        item.Rendered = false;
                        Logger.LogError($"Failed to render subclip: {item.VideoFile} from {item.StartTime} to {item.EndTime}. Error: {ex.Message}", "GyroFlow Render Queue");
                    }
                }
                // Dequeue all items that were processed
                foreach (var item in itemsToDequeue)
                {
                    Dequeue(item);
                }
            });
        }

        public static void Enqueue(SubclipInfo info)
        {
            //Threadsafe enqueue
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                //Check if the subclip already exists in the queue
                if (Subclips.Any(x => x.VideoFile == info.VideoFile && x.StartTime == info.StartTime && x.EndTime == info.EndTime))
                {
                    Logger.LogDebug("Subclip already exists in the render queue.", "GyroFlow Render Queue");
                    return;
                }
                //Check if the subclip is valid
                if (string.IsNullOrEmpty(info.VideoFile) || !System.IO.File.Exists(info.VideoFile))
                {
                    throw new ArgumentException("Invalid video file path provided.");
                }
                Subclips.Add(info);
            });
        }

        public static void Dequeue(SubclipInfo info)
        {
            //threadsafe dequeue
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (Subclips.Contains(info))
                {
                    Subclips.Remove(info);
                }
            });
        }
    }
}
