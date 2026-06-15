using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ClipCull.Core;
using ClipCull.Core.Rendering;
using ClipCull.Models;

namespace ClipCull.Controls
{
    public partial class RenderQueueControl : UserControl, INotifyPropertyChanged
    {
        private bool _isRendering;

        public RenderQueueControl()
        {
            InitializeComponent();
            RenderQueue.Jobs.CollectionChanged += Jobs_CollectionChanged;
            foreach (var job in RenderQueue.Jobs)
            {
                job.PropertyChanged += Job_PropertyChanged;
            }
            Unloaded += RenderQueueControl_Unloaded;
            PopulateEngineSelector();
            UpdateUI();
            DataContext = this;
        }

        private void RenderQueueControl_Unloaded(object sender, RoutedEventArgs e)
        {
            RenderQueue.Jobs.CollectionChanged -= Jobs_CollectionChanged;
            foreach (var job in RenderQueue.Jobs)
            {
                job.PropertyChanged -= Job_PropertyChanged;
            }
        }

        private void Jobs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (RenderJobInfo oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= Job_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (RenderJobInfo newItem in e.NewItems)
                {
                    newItem.PropertyChanged += Job_PropertyChanged;
                }
            }
            UpdateUI();
        }

        public bool IsRendering
        {
            get => _isRendering;
            set
            {
                _isRendering = value;
                OnPropertyChanged();
                UpdateUI();
            }
        }

        public int QueueCount => RenderQueue.Jobs.Count;

        public bool IsQueueEmpty => QueueCount == 0;

        private void PopulateEngineSelector()
        {
            CbRenderEngine.Items.Clear();
            foreach (var engine in RenderEngineFactory.GetAllEngines())
            {
                CbRenderEngine.Items.Add(new ComboBoxItem
                {
                    Content = engine.Name,
                    Tag = engine.EngineType
                });
            }

            // Select current default engine
            var settings = SettingsHandler.Settings.DefaultRenderSettings;
            if (settings != null)
            {
                foreach (ComboBoxItem item in CbRenderEngine.Items)
                {
                    if (item.Tag is RenderEngineType type && type == settings.Engine)
                    {
                        CbRenderEngine.SelectedItem = item;
                        break;
                    }
                }
            }

            if (CbRenderEngine.SelectedItem == null && CbRenderEngine.Items.Count > 0)
                CbRenderEngine.SelectedIndex = 0;
        }

        private void UpdateUI()
        {
            var settings = SettingsHandler.Settings.DefaultRenderSettings;
            var engineType = settings?.Engine ?? RenderEngineType.Gyroflow;

            try
            {
                var engine = RenderEngineFactory.Create(engineType);
                TxEngineStatus.Text = engine.GetStatusDescription();
                TxRenderingEngineInfo.Text = $"Processing video clips with {engine.Name}";
            }
            catch
            {
                TxEngineStatus.Text = "No engine available";
            }

            StartRenderButton.IsEnabled = !IsRendering && RenderQueue.Jobs.Count > 0;
            CancelRenderButton.Visibility = IsRendering ? Visibility.Visible : Visibility.Collapsed;
            UpdateRenderingProgressText();
            OnPropertyChanged(nameof(QueueCount));
            OnPropertyChanged(nameof(IsQueueEmpty));
        }

        private void Job_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RenderJobInfo.Rendering))
            {
                if (sender is RenderJobInfo job && job.Rendering)
                {
                    ScrollToJob(job);
                }
                UpdateRenderingProgressText();
            }
        }

        private void ScrollToJob(RenderJobInfo job)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = QueueItemsControl.ItemContainerGenerator.ContainerFromItem(job) as FrameworkElement;
                if (container != null)
                {
                    container.BringIntoView();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateRenderingProgressText()
        {
            var jobs = RenderQueue.Jobs;
            int total = jobs.Count;
            if (total == 0)
            {
                TxRenderingProgress.Text = "";
                return;
            }

            var currentJob = jobs.FirstOrDefault(j => j.Rendering);
            int currentIndex = currentJob != null ? jobs.IndexOf(currentJob) + 1 : 1;

            TxRenderingProgress.Text = $"Clip {currentIndex}/{total}";
        }

        private void CbRenderEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbRenderEngine.SelectedItem is ComboBoxItem item && item.Tag is RenderEngineType type)
            {
                var settings = SettingsHandler.Settings.DefaultRenderSettings;
                if (settings == null)
                {
                    settings = new RenderSettings();
                    SettingsHandler.Settings.DefaultRenderSettings = settings;
                }
                settings.Engine = type;
                UpdateUI();
            }
        }

        private async void StartRenderButton_Click(object sender, RoutedEventArgs e)
        {
            IsRendering = true;

            try
            {
                await RenderQueue.RenderAllAsync();
                Logger.LogSuccess("All items in the render queue have been processed.", "Render Queue");
            }
            catch (Exception ex)
            {
                Logger.LogError("An error occurred while processing the render queue.", ex, "Render Queue");
            }
            finally
            {
                IsRendering = false;
            }
        }

        private void CancelRenderButton_Click(object sender, RoutedEventArgs e)
        {
            RenderQueue.Cancel();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is RenderJobInfo job)
            {
                RenderQueue.Dequeue(job);
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
