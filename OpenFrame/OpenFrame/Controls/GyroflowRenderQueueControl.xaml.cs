using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using OpenFrame.Core;
using OpenFrame.Core.Gyroflow;
using OpenFrame.Extensions;
using static OpenFrame.Core.Gyroflow.GyroflowSubclipExtractor;

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
            GyroFlowRenderQueue.Subclips.CollectionChanged += Subclips_CollectionChanged;
            UpdateUI();
            DataContext = this;
        }

        private void Subclips_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateUI();
        }

        public bool IsRendering
        {
            get => _isRendering;
            set
            {
                _isRendering = value;
                OnPropertyChanged();

                // Update button state
                UpdateUI();
            }
        }

        public int QueueCount => GyroFlowRenderQueue.Subclips.Count;

        public bool IsQueueEmpty => QueueCount == 0;

        private void UpdateUI()
        {
            StartRenderButton.IsEnabled = !IsRendering && GyroFlowRenderQueue.Subclips.Count > 0;
            OnPropertyChanged(nameof(QueueCount));
            OnPropertyChanged(nameof(IsQueueEmpty));
        }

        private async void StartRenderButton_Click(object sender, RoutedEventArgs e)
        {
            IsRendering = true;

            try
            {
                await GyroFlowRenderQueue.RenderAllItemsInQueue();
                Logger.LogSuccess("All items in the render queue have been processed.", "GyroFlow Render Queue");
            }
            catch (Exception ex)
            {
                Logger.LogError("An error occurred while processing the render queue.", ex, "GyroFlow Render Queue");
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