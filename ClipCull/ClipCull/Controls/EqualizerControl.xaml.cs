using ClipCull.Core;
using ClipCull.Models;
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

                var col = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };

                var freqLabel = new TextBlock
                {
                    Text = FormatFrequency(freq),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Style = (Style)FindResource("muted")
                };

                var slider = new Slider
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    Minimum = EqualizerSettings.MinGainDb,
                    Maximum = EqualizerSettings.MaxGainDb,
                    Value = 0,
                    Height = 140,
                    TickFrequency = 1,
                    Tag = i
                };
                slider.ValueChanged += BandSlider_ValueChanged;

                var valueLabel = new TextBlock
                {
                    Text = FormatDb(0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    Style = (Style)FindResource("muted"),
                    MinWidth = 50,
                    TextAlignment = TextAlignment.Center
                };

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
            eq.BandGainsDb[index] = e.NewValue;
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

        private static string FormatDb(double db) => $"{db:+0.0;-0.0;0.0} dB";

        private static string FormatFrequency(int hz) =>
            hz >= 1000 ? $"{hz / 1000}k" : hz.ToString();
    }
}
