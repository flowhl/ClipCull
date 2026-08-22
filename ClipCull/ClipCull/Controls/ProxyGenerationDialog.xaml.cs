using ClipCull.Core;
using ClipCull.Core.Proxy;
using ClipCull.Extensions;
using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ClipCull.Controls
{
    /// <summary>
    /// Dialog that lets the user pick videos in a folder tree (folder + subfolders) and generate
    /// downscaled proxy files for smooth preview, showing live progress and a time estimate.
    /// </summary>
    public partial class ProxyGenerationDialog : Window
    {
        private readonly string _folderPath;
        private readonly ObservableCollection<ProxyFileItem> _items = new ObservableCollection<ProxyFileItem>();
        private CancellationTokenSource _cts;
        private bool _isRunning;

        /// <summary>True if at least one proxy was successfully created, so the caller can refresh its view.</summary>
        public bool AnyGenerated { get; private set; }

        public ProxyGenerationDialog(string folderPath)
        {
            InitializeComponent();
            _folderPath = folderPath;

            PopulateResolutionOptions();
            PopulateFrameRateOptions();

            var s = SettingsHandler.Settings;
            ChkHardware.IsChecked = s.ProxyUseHardwareEncoding;
            ChkSkipExisting.IsChecked = s.ProxySkipExisting;

            FileList.ItemsSource = _items;

            Loaded += ProxyGenerationDialog_Loaded;
            Closing += ProxyGenerationDialog_Closing;
        }

        private void ProxyGenerationDialog_Loaded(object sender, RoutedEventArgs e)
        {
            SubtitleLabel.Text = _folderPath;
            LoadFiles();
        }

        private void LoadFiles()
        {
            _items.Clear();
            var files = ProxyService.FindVideoFiles(_folderPath, recursive: true);
            foreach (var file in files)
            {
                var item = new ProxyFileItem
                {
                    FullPath = file,
                    DisplayName = GetRelativeDisplayName(file),
                    HasProxy = ProxyService.HasProxy(file),
                    IsSelected = true
                };
                item.PropertyChanged += Item_PropertyChanged;
                _items.Add(item);
            }

            if (_items.Count == 0)
            {
                SubtitleLabel.Text = $"No video files found in {_folderPath}";
                BtnGenerate.IsEnabled = false;
            }

            UpdateSelectionCount();
        }

        private string GetRelativeDisplayName(string file)
        {
            try
            {
                string rel = Path.GetRelativePath(_folderPath, file);
                return string.IsNullOrEmpty(rel) ? Path.GetFileName(file) : rel;
            }
            catch
            {
                return Path.GetFileName(file);
            }
        }

        private void PopulateResolutionOptions()
        {
            AddComboItem(CbResolution, "1080p (1920×1080)", 1080);
            AddComboItem(CbResolution, "720p (1280×720)", 720);
            AddComboItem(CbResolution, "540p (960×540)", 540);
            AddComboItem(CbResolution, "480p (854×480)", 480);
            SelectByTag(CbResolution, SettingsHandler.Settings.ProxyResolutionHeight, defaultTag: 720);
        }

        private void PopulateFrameRateOptions()
        {
            AddComboItem(CbFrameRate, "Original", 0);
            AddComboItem(CbFrameRate, "60 fps", 60);
            AddComboItem(CbFrameRate, "30 fps", 30);
            SelectByTag(CbFrameRate, SettingsHandler.Settings.ProxyFrameRate, defaultTag: 30);
        }

        private static void AddComboItem(ComboBox combo, string text, int tag)
        {
            combo.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
        }

        private static void SelectByTag(ComboBox combo, int tag, int defaultTag)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is int t && t == tag)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is int t && t == defaultTag)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static int SelectedTag(ComboBox combo)
        {
            return combo.SelectedItem is ComboBoxItem item && item.Tag is int t ? t : 0;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProxyFileItem.IsSelected))
                UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int selected = _items.Count(i => i.IsSelected);
            SelectionCountLabel.Text = $"{selected} of {_items.Count} selected";
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                return;
            bool select = ChkSelectAll.IsChecked == true;
            foreach (var item in _items)
                item.IsSelected = select;
        }

        private void ChkSkipExisting_Changed(object sender, RoutedEventArgs e)
        {
            // No-op beyond reading the value at generation time; handler exists so the checkbox works.
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                return;

            var selectedFiles = _items.Where(i => i.IsSelected).Select(i => i.FullPath).ToList();
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Select at least one file.", "Generate Proxies",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(Globals.FFmpegPath))
            {
                MessageBox.Show($"FFmpeg was not found at:\n{Globals.FFmpegPath}", "Generate Proxies",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var options = new ProxyOptions
            {
                Height = SelectedTag(CbResolution),
                FrameRate = SelectedTag(CbFrameRate),
                UseHardwareEncoding = ChkHardware.IsChecked == true,
                SkipExisting = ChkSkipExisting.IsChecked == true
            };

            // Persist choices for next time.
            var s = SettingsHandler.Settings;
            s.ProxyResolutionHeight = options.Height;
            s.ProxyFrameRate = options.FrameRate;
            s.ProxyUseHardwareEncoding = options.UseHardwareEncoding;
            s.ProxySkipExisting = options.SkipExisting;
            SettingsHandler.Save();

            SetRunningState(true);

            _cts = new CancellationTokenSource();
            var progress = new Progress<ProxyBatchProgress>(UpdateProgressUI);
            var generator = new ProxyGenerator();

            ProxyBatchResult result = null;
            try
            {
                result = await generator.GenerateAsync(selectedFiles, options, progress, _cts.Token);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Proxy generation failed: {ex.GetFullDetails()}");
                MessageBox.Show($"Proxy generation failed:\n{ex.Message}", "Generate Proxies",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            SetRunningState(false);

            if (result != null)
            {
                AnyGenerated = AnyGenerated || result.Succeeded > 0;
                ShowSummary(result);
                RefreshProxyFlags();
            }
        }

        private void UpdateProgressUI(ProxyBatchProgress p)
        {
            OverallStatusLabel.Text =
                $"Processing {p.CurrentFileIndex} of {p.TotalFiles}" +
                (p.Eta.HasValue ? $"  •  ETA {Format(p.Eta.Value)}" : "") +
                $"  •  Elapsed {Format(p.Elapsed)}";
            OverallProgress.Value = p.OverallPercent;

            string speed = p.Speed.HasValue ? $"  ({p.Speed.Value:F1}×)" : "";
            CurrentFileLabel.Text = $"{p.CurrentFileName} — {p.CurrentFilePercent:F0}%{speed}";
            CurrentProgress.Value = p.CurrentFilePercent;
        }

        private static string Format(TimeSpan t)
        {
            return t.TotalHours >= 1
                ? t.ToString(@"h\:mm\:ss")
                : t.ToString(@"m\:ss");
        }

        private void ShowSummary(ProxyBatchResult result)
        {
            var parts = new List<string>();
            if (result.Succeeded > 0) parts.Add($"{result.Succeeded} created");
            if (result.Skipped > 0) parts.Add($"{result.Skipped} skipped");
            if (result.Failed > 0) parts.Add($"{result.Failed} failed");
            if (result.Cancelled) parts.Add("cancelled");

            OverallStatusLabel.Text = parts.Count > 0 ? string.Join("  •  ", parts) : "Nothing to do";
            CurrentFileLabel.Text = result.Errors.Count > 0
                ? $"First error: {result.Errors[0]}"
                : "Done";

            if (result.Errors.Count > 0)
                Logger.LogError("Proxy errors:\n" + string.Join("\n", result.Errors));
        }

        private void RefreshProxyFlags()
        {
            foreach (var item in _items)
                item.HasProxy = ProxyService.HasProxy(item.FullPath);
        }

        private void SetRunningState(bool running)
        {
            _isRunning = running;

            CbResolution.IsEnabled = !running;
            CbFrameRate.IsEnabled = !running;
            ChkHardware.IsEnabled = !running;
            ChkSkipExisting.IsEnabled = !running;
            ChkSelectAll.IsEnabled = !running;
            // Don't disable the ListBox itself: WPF's default disabled state paints it white and
            // drops the item foreground, making the file names unreadable. Instead make it
            // non-interactive and dim it slightly so it still reads as "locked" during a run.
            FileList.IsHitTestVisible = !running;
            FileList.Opacity = running ? 0.55 : 1.0;
            BtnGenerate.IsEnabled = !running && _items.Count > 0;

            // Reveal the progress/summary area once a run starts and keep it visible afterwards so
            // the final summary (including all-skipped or all-failed) stays on screen.
            if (running)
                ProgressPanel.Visibility = Visibility.Visible;
            BtnClose.Content = running ? "Cancel" : "Close";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _cts?.Cancel();
                return;
            }
            Close();
        }

        private void ProxyGenerationDialog_Closing(object sender, CancelEventArgs e)
        {
            if (_isRunning)
            {
                // Don't tear down mid-encode; cancel first and let the batch unwind.
                _cts?.Cancel();
                e.Cancel = true;
            }
        }
    }

    /// <summary>
    /// One selectable row in the proxy dialog's file list.
    /// </summary>
    public class ProxyFileItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _hasProxy;

        public string FullPath { get; set; }
        public string DisplayName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        public bool HasProxy
        {
            get => _hasProxy;
            set { if (_hasProxy != value) { _hasProxy = value; OnPropertyChanged(nameof(HasProxy)); OnPropertyChanged(nameof(HasProxyVisibility)); } }
        }

        public Visibility HasProxyVisibility => HasProxy ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
