using ClipCull.Core;
using ClipCull.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClipCull.Controls
{
    /// <summary>
    /// Interaction logic for EqualizerControl.xaml
    /// </summary>
    public partial class EqualizerControl : UserControl
    {
        public static readonly DependencyProperty EqualizerProperty =
            DependencyProperty.Register(
                nameof(Equalizer),
                typeof(EqualizerSettings),
                typeof(EqualizerControl),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnEqualizerChanged));

        public EqualizerSettings Equalizer
        {
            get => (EqualizerSettings)GetValue(EqualizerProperty);
            set => SetValue(EqualizerProperty, value);
        }

        private bool _isSyncing;
        private readonly Slider[] _bandSliders = new Slider[EqualizerSettings.BandCount];
        private readonly TextBlock[] _bandLabels = new TextBlock[EqualizerSettings.BandCount];

        public EqualizerControl()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                BuildBandSliders();
                ToggleEqualizerEnabled.Toggled -= ToggleEqualizerEnabled_Toggled;
                ToggleEqualizerEnabled.Toggled += ToggleEqualizerEnabled_Toggled;
                SliderPreamp.MouseDoubleClick += (s, e) => { SliderPreamp.Value = 0; };
                SyncFromModel();
            };
        }

        private static void OnEqualizerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EqualizerControl control)
                control.SyncFromModel();
        }

        private void BuildBandSliders()
        {
            if (EqBandsGrid.Children.Count > 0)
                return;

            for (int i = 0; i < EqualizerSettings.BandCount; i++)
            {
                int freq = EqualizerSettings.BandFrequenciesHz[i];

                var col = new Grid
                {
                    Margin = new Thickness(2, 0, 2, 0)
                };
                col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var freqLabel = new TextBlock
                {
                    Text = FormatFrequency(freq),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Style = (Style)FindResource("muted"),
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                Grid.SetRow(freqLabel, 0);

                var slider = new Slider
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    Minimum = EqualizerSettings.MinGainDb,
                    Maximum = EqualizerSettings.MaxGainDb,
                    Value = 0,
                    MinHeight = 100,
                    MaxHeight = 200,
                    TickFrequency = 1,
                    Tag = i,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                slider.ValueChanged += BandSlider_ValueChanged;
                slider.MouseDoubleClick += (s, e) => { if (s is Slider sl) sl.Value = 0; };
                Grid.SetRow(slider, 1);

                var valueLabel = new TextBlock
                {
                    Text = FormatDb(0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0),
                    Style = (Style)FindResource("muted"),
                    MinWidth = 40,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 10
                };
                Grid.SetRow(valueLabel, 2);

                col.Children.Add(freqLabel);
                col.Children.Add(slider);
                col.Children.Add(valueLabel);

                _bandSliders[i] = slider;
                _bandLabels[i] = valueLabel;

                EqBandsGrid.Children.Add(col);
            }
        }

        private void SyncFromModel()
        {
            if (!IsLoaded || _bandSliders[0] == null)
                return;

            _isSyncing = true;
            try
            {
                var eq = Equalizer;
                if (eq == null)
                {
                    ToggleEqualizerEnabled.IsToggled = false;
                    SliderPreamp.Value = 0;
                    LblPreamp.Text = FormatDb(0);
                    for (int i = 0; i < EqualizerSettings.BandCount; i++)
                    {
                        _bandSliders[i].Value = 0;
                        _bandLabels[i].Text = FormatDb(0);
                    }
                    return;
                }

                ToggleEqualizerEnabled.IsToggled = eq.Enabled;
                SliderPreamp.Value = eq.PreampDb;
                LblPreamp.Text = FormatDb(eq.PreampDb);

                var gains = eq.BandGainsDb;
                for (int i = 0; i < EqualizerSettings.BandCount; i++)
                {
                    double v = (gains != null && i < gains.Length) ? gains[i] : 0.0;
                    _bandSliders[i].Value = v;
                    _bandLabels[i].Text = FormatDb(v);
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void ToggleEqualizerEnabled_Toggled(object sender, bool isToggled)
        {
            if (_isSyncing) return;
            var eq = Equalizer;
            if (eq != null)
                eq.Enabled = isToggled;
        }

        private void SliderPreamp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LblPreamp != null)
                LblPreamp.Text = FormatDb(e.NewValue);
            if (_isSyncing) return;
            var eq = Equalizer;
            if (eq != null)
                eq.PreampDb = e.NewValue;
        }

        private void BandSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is not Slider s || s.Tag is not int index)
                return;

            if (_bandLabels[index] != null)
                _bandLabels[index].Text = FormatDb(e.NewValue);

            if (_isSyncing) return;
            var eq = Equalizer;
            if (eq == null) return;

            if (eq.BandGainsDb == null || eq.BandGainsDb.Length != EqualizerSettings.BandCount)
                eq.BandGainsDb = new double[EqualizerSettings.BandCount];
            
            eq.SetBandGain(index, e.NewValue);
        }

        private void BtnEqReset_Click(object sender, RoutedEventArgs e)
        {
            var eq = Equalizer;
            if (eq == null) return;
            eq.PreampDb = 0;
            eq.BandGainsDb = new double[EqualizerSettings.BandCount];
            SyncFromModel();
        }

        private void BtnEqCopy_Click(object sender, RoutedEventArgs e)
        {
            EqualizerClipboard.Copy(Equalizer);
            Logger.LogInfo("Equalizer settings copied to clipboard.");
        }

        private void BtnEqPaste_Click(object sender, RoutedEventArgs e)
        {
            if (Equalizer == null)
            {
                Logger.LogWarning("No equalizer target to paste into.");
                return;
            }
            if (!EqualizerClipboard.HasContent)
            {
                Logger.LogWarning("Equalizer clipboard is empty.");
                return;
            }
            EqualizerClipboard.PasteInto(Equalizer);
            SyncFromModel();
            Logger.LogInfo("Equalizer settings pasted from clipboard.");
        }

        private void BtnEqApplyAll_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not VideoPreviewControl preview || string.IsNullOrEmpty(preview.CurrentVideoPath))
            {
                Logger.LogWarning("No video loaded. Cannot apply EQ to folder.");
                return;
            }

            var result = MessageBox.Show(
                "This will apply the current equalizer settings to ALL video files in this folder by modifying their sidecar files. Existing EQ settings for those files will be overwritten.\n\nContinue?",
                "Apply EQ to Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string directory = Path.GetDirectoryName(preview.CurrentVideoPath);
                if (string.IsNullOrEmpty(directory)) return;

                var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };
                var videoFiles = Directory.GetFiles(directory)
                    .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                int count = 0;
                foreach (var file in videoFiles)
                {
                    // Skip the current file if it's already updated (though updating it again is harmless)
                    if (file.Equals(preview.CurrentVideoPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var sidecar = SidecarService.GetSidecarContent(file);
                    sidecar.Equalizer = EqualizerClipboard.Clone(Equalizer);
                    SidecarService.SaveSidecarContent(sidecar, file);
                    count++;
                }

                Logger.LogInfo($"Successfully applied EQ settings to {count} other video(s) in folder.");
                MessageBox.Show($"Successfully applied EQ settings to {count} other video files in the folder.", 
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply EQ to folder", ex);
                MessageBox.Show($"Failed to apply EQ to folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEqRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not VideoPreviewControl preview || string.IsNullOrEmpty(preview.CurrentVideoPath))
            {
                Logger.LogWarning("No video loaded. Cannot remove EQ from folder.");
                return;
            }

            var result = MessageBox.Show(
                "This will DISABLE and RESET the equalizer settings for ALL video files in this folder. All custom EQ data for these clips will be lost.\n\nContinue?",
                "Remove EQ from Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string directory = Path.GetDirectoryName(preview.CurrentVideoPath);
                if (string.IsNullOrEmpty(directory)) return;

                var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };
                var videoFiles = Directory.GetFiles(directory)
                    .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                int count = 0;
                foreach (var file in videoFiles)
                {
                    var sidecar = SidecarService.GetSidecarContent(file);
                    sidecar.Equalizer = new EqualizerSettings { Enabled = false };
                    SidecarService.SaveSidecarContent(sidecar, file);
                    count++;
                    
                    // If we just updated the current file's sidecar, refresh the live UI
                    if (file.Equals(preview.CurrentVideoPath, StringComparison.OrdinalIgnoreCase))
                    {
                        preview.Equalizer = sidecar.Equalizer;
                        SyncFromModel();
                    }
                }

                Logger.LogInfo($"Successfully removed EQ settings from {count} video(s) in folder.");
                MessageBox.Show($"Successfully removed EQ settings from {count} video files in the folder.", 
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove EQ from folder", ex);
                MessageBox.Show($"Failed to remove EQ from folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string FormatDb(double db) => $"{db:+0.0;-0.0;0.0} dB";

        private static string FormatFrequency(int hz) =>
            hz >= 1000 ? $"{hz / 1000}k" : hz.ToString();
    }
}
