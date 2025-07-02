using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpenFrame.Core.Gyroflow;
using static OpenFrame.Core.Gyroflow.GyroflowSubclipExtractor;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace OpenFrame.Controls
{
    /// <summary>
    /// Interaction logic for GyroflowRenderQueueControl.xaml
    /// </summary>
    public partial class GyroflowRenderQueueControl : UserControl, INotifyPropertyChanged
    {
        private bool _isRendering;

        public GyroflowRenderQueueControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public bool IsRendering
        {
            get => _isRendering;
            set
            {
                _isRendering = value;
                OnPropertyChanged();

                // Update button state
                StartRenderButton.IsEnabled = !value && GyroFlowRenderQueue.Subclips.Count > 0;
            }
        }

        public int QueueCount => GyroFlowRenderQueue.Subclips.Count;

        public bool IsQueueEmpty => QueueCount == 0;

        private async void StartRenderButton_Click(object sender, RoutedEventArgs e)
        {
            if (GyroFlowRenderQueue.Subclips.Count == 0)
            {
                MessageBox.Show("No items in the render queue.", "Render Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsRendering = true;

            try
            {
                // Create a copy of the queue items to process
                var itemsToRender = GyroFlowRenderQueue.Subclips.ToList();

                // TODO: Replace this with actual Gyroflow rendering
                // For now, simulate rendering with Thread.Sleep as requested
                await Task.Run(() =>
                {
                    foreach (var item in itemsToRender)
                    {
                        // Simulate processing time per item
                        Thread.Sleep(2000); // Replace with actual rendering call

                        // Remove completed item from queue (on UI thread)
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            GyroFlowRenderQueue.Dequeue(item);
                            OnPropertyChanged(nameof(QueueCount));
                            OnPropertyChanged(nameof(IsQueueEmpty));
                        });
                    }
                });

                MessageBox.Show($"Successfully rendered {itemsToRender.Count} clips!", "Render Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rendering failed: {ex.Message}", "Render Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRendering = false;
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SubclipInfo subclip)
            {
                GyroFlowRenderQueue.Dequeue(subclip);
                OnPropertyChanged(nameof(QueueCount));
                OnPropertyChanged(nameof(IsQueueEmpty));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}