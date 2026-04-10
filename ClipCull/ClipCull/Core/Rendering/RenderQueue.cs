using ClipCull.Extensions;
using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ClipCull.Core.Rendering
{
    public static class RenderQueue
    {
        public static ObservableCollection<RenderJobInfo> Jobs { get; } = new ObservableCollection<RenderJobInfo>();

        private static CancellationTokenSource _cancellationTokenSource;

        public static bool IsRendering { get; private set; }

        public static async Task RenderAllAsync()
        {
            if (Jobs.Count == 0)
            {
                Logger.LogWarning("Render queue is empty.", "Render Queue");
                return;
            }

            if (Jobs.Any(x => x.OutputName.IsNullOrEmpty()))
            {
                Logger.LogWarning("Some jobs do not have an output name set.", "Render Queue");
                return;
            }

            string outputDirectory = DialogHelper.ChooseFolder("Select Output Directory for Render");
            if (outputDirectory.IsNullOrEmpty() || !Directory.Exists(outputDirectory))
            {
                return;
            }

            // Check if any of the files should be overwritten
            List<string> existingFiles = Jobs
                .Select(x => Path.Combine(outputDirectory, x.OutputName))
                .Where(x => File.Exists(x))
                .ToList();

            bool overwrite = false;

            if (existingFiles.Count > 0)
            {
                var result = MessageBox.Show(
                    "Some output files already exist. Do you want to overwrite them?",
                    "Overwrite Existing Files",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel)
                {
                    Logger.LogWarning("Render cancelled by user.", "Render Queue");
                    return;
                }
                overwrite = result == MessageBoxResult.Yes;
            }

            var settings = SettingsHandler.Settings.DefaultRenderSettings ?? new RenderSettings();
            IRenderEngine engine;
            try
            {
                engine = RenderEngineFactory.Create(settings.Engine);
            }
            catch (ArgumentException)
            {
                Logger.LogError($"No render engine available for type: {settings.Engine}", "Render Queue");
                return;
            }

            if (!await engine.IsAvailableAsync())
            {
                Logger.LogError($"{engine.Name} is not available: {engine.GetStatusDescription()}", "Render Queue");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            IsRendering = true;

            await Task.Run(async () =>
            {
                var itemsToDequeue = new List<RenderJobInfo>();
                foreach (var item in Jobs)
                {
                    if (_cancellationTokenSource.IsCancellationRequested)
                    {
                        Logger.LogWarning("Render cancelled by user.", "Render Queue");
                        break;
                    }

                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.Rendered = false;
                            item.Rendering = true;
                            item.HasError = false;
                            item.ErrorMessage = null;
                            item.ProgressPercentage = 0;
                        });

                        string output = await engine.RenderAsync(
                            item,
                            settings,
                            outputDirectory,
                            overwrite,
                            progress =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    item.Progress = progress;
                                    item.ProgressPercentage = progress.Percentage;
                                    item.Status = progress.DisplayText;
                                });
                            },
                            _cancellationTokenSource.Token);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.Rendering = false;
                            item.Rendered = true;
                            item.ProgressPercentage = 100;
                        });

                        itemsToDequeue.Add(item);
                        Logger.LogDebug($"Rendered: {item.VideoFile} ({item.StartTime} - {item.EndTime}) -> {output}", "Render Queue");
                    }
                    catch (OperationCanceledException)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.Rendering = false;
                            item.Status = "Cancelled";
                        });
                        break;
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.Rendering = false;
                            item.Rendered = false;
                            item.HasError = true;
                            item.ErrorMessage = ex.Message;
                            item.Status = null;
                        });
                        Logger.LogError($"Failed to render: {item.VideoFile} ({item.StartTime} - {item.EndTime}). Error: {ex.Message}", "Render Queue");
                    }
                }

                // Dequeue all items that were processed
                foreach (var item in itemsToDequeue)
                {
                    Dequeue(item);
                }
            });

            IsRendering = false;
            _cancellationTokenSource = null;
        }

        public static void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public static void Enqueue(RenderJobInfo info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Jobs.Any(x => x.VideoFile == info.VideoFile && x.StartTime == info.StartTime && x.EndTime == info.EndTime))
                {
                    Logger.LogDebug("Job already exists in the render queue.", "Render Queue");
                    return;
                }
                if (string.IsNullOrEmpty(info.VideoFile) || !File.Exists(info.VideoFile))
                {
                    throw new ArgumentException("Invalid video file path provided.");
                }
                Jobs.Add(info);
            });
        }

        public static void Dequeue(RenderJobInfo info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Jobs.Contains(info))
                {
                    Jobs.Remove(info);
                }
            });
        }
    }
}
