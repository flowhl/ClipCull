using ClipCull.Core;
using ClipCull.Core.Rendering;
using ClipCull.Extensions;
using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClipCull.Controls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private bool _isLoading;

        public SettingsControl()
        {
            InitializeComponent();
            Loaded += SettingsControl_Loaded;
        }

        private void SettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

            SettingsHandler.Load();
            DataContext = SettingsHandler.Settings;
            TxCurrentGyroflowPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowPath ?? "Discoved automatically");
            TxCurrentGyroflowSettingsPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowSettingsPath ?? "Using Default");

            PopulateRenderSettingsControls();

            //Tags
            var tagCollection = new ObservableCollection<EditableTag>();
            SettingsHandler.Settings.Tags.ForEach(x => tagCollection.Add(new EditableTag
            {
                Color = x.Color,
                Name = x.Name
            }));
            TagManagement.Tags = tagCollection;

            _isLoading = false;
        }

        private void PopulateRenderSettingsControls()
        {
            var settings = SettingsHandler.Settings.DefaultRenderSettings ?? new RenderSettings();

            // Engine
            CbDefaultEngine.Items.Clear();
            foreach (var engine in RenderEngineFactory.GetAllEngines())
            {
                var item = new ComboBoxItem { Content = engine.Name, Tag = engine.EngineType };
                CbDefaultEngine.Items.Add(item);
                if (engine.EngineType == settings.Engine)
                    CbDefaultEngine.SelectedItem = item;
            }

            // Video Codec
            CbVideoCodec.Items.Clear();
            foreach (VideoCodec codec in Enum.GetValues(typeof(VideoCodec)))
            {
                var item = new ComboBoxItem { Content = codec.ToString(), Tag = codec };
                CbVideoCodec.Items.Add(item);
                if (codec == settings.VideoCodec)
                    CbVideoCodec.SelectedItem = item;
            }

            // Quality Mode
            CbQualityMode.Items.Clear();
            foreach (QualityMode mode in Enum.GetValues(typeof(QualityMode)))
            {
                var item = new ComboBoxItem { Content = mode.ToString(), Tag = mode };
                CbQualityMode.Items.Add(item);
                if (mode == settings.QualityMode)
                    CbQualityMode.SelectedItem = item;
            }

            // Quality value
            TbQuality.Text = settings.Quality.ToString();

            // Audio Codec
            CbAudioCodec.Items.Clear();
            foreach (AudioCodec codec in Enum.GetValues(typeof(AudioCodec)))
            {
                var item = new ComboBoxItem { Content = codec.ToString(), Tag = codec };
                CbAudioCodec.Items.Add(item);
                if (codec == settings.AudioCodec)
                    CbAudioCodec.SelectedItem = item;
            }

            // Container Format
            CbContainerFormat.Items.Clear();
            foreach (ContainerFormat format in Enum.GetValues(typeof(ContainerFormat)))
            {
                var item = new ComboBoxItem { Content = format.ToString(), Tag = format };
                CbContainerFormat.Items.Add(item);
                if (format == settings.ContainerFormat)
                    CbContainerFormat.SelectedItem = item;
            }

            // Hardware Acceleration
            CbHardwareAcceleration.Items.Clear();
            foreach (HardwareAcceleration accel in Enum.GetValues(typeof(HardwareAcceleration)))
            {
                var item = new ComboBoxItem { Content = accel.ToString(), Tag = accel };
                CbHardwareAcceleration.Items.Add(item);
                if (accel == settings.HardwareAcceleration)
                    CbHardwareAcceleration.SelectedItem = item;
            }

            // Preset
            CbPreset.Items.Clear();
            string[] presets = { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" };
            foreach (string preset in presets)
            {
                var item = new ComboBoxItem { Content = preset, Tag = preset };
                CbPreset.Items.Add(item);
                if (preset == settings.Preset)
                    CbPreset.SelectedItem = item;
            }

            // AME Path
            TxCurrentAMEPath.Text = "Path: " + (SettingsHandler.Settings.AdobeMediaEncoderPath ?? "Auto-discover");
        }

        private RenderSettings EnsureRenderSettings()
        {
            if (SettingsHandler.Settings.DefaultRenderSettings == null)
                SettingsHandler.Settings.DefaultRenderSettings = new RenderSettings();
            return SettingsHandler.Settings.DefaultRenderSettings;
        }

        private void CbDefaultEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbDefaultEngine.SelectedItem is ComboBoxItem item && item.Tag is RenderEngineType type)
                EnsureRenderSettings().Engine = type;
        }

        private void CbVideoCodec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbVideoCodec.SelectedItem is ComboBoxItem item && item.Tag is VideoCodec codec)
                EnsureRenderSettings().VideoCodec = codec;
        }

        private void CbQualityMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbQualityMode.SelectedItem is ComboBoxItem item && item.Tag is QualityMode mode)
                EnsureRenderSettings().QualityMode = mode;
        }

        private void CbAudioCodec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbAudioCodec.SelectedItem is ComboBoxItem item && item.Tag is AudioCodec codec)
                EnsureRenderSettings().AudioCodec = codec;
        }

        private void CbContainerFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbContainerFormat.SelectedItem is ComboBoxItem item && item.Tag is ContainerFormat format)
                EnsureRenderSettings().ContainerFormat = format;
        }

        private void CbHardwareAcceleration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbHardwareAcceleration.SelectedItem is ComboBoxItem item && item.Tag is HardwareAcceleration accel)
                EnsureRenderSettings().HardwareAcceleration = accel;
        }

        private void CbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (CbPreset.SelectedItem is ComboBoxItem item && item.Tag is string preset)
                EnsureRenderSettings().Preset = preset;
        }

        private void BtnPickAMEExe_Click(object sender, RoutedEventArgs e)
        {
            string amePath = DialogHelper.ChooseFile("Select Adobe Media Encoder Executable", "Executable|*.exe");
            if (amePath.IsNullOrEmpty())
                return;
            SettingsHandler.Settings.AdobeMediaEncoderPath = amePath;
            TxCurrentAMEPath.Text = "Path: " + amePath;
        }

        public void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var newTags = new List<Tag>();
            TagManagement.Tags.ToList().ForEach(x => newTags.Add(new Models.Tag
            {
                Color = x.Color,
                Name = x.Name
            }));

            SettingsHandler.Settings.Tags = newTags;

            // Save quality value from textbox
            if (int.TryParse(TbQuality.Text, out int quality))
                EnsureRenderSettings().Quality = quality;

            SettingsHandler.Save();
            HotkeyController.SaveMappings();
            Logger.LogSuccess("Settings saved successfully.");
        }

        private void BtnPickGyroflowExe_Click(object sender, RoutedEventArgs e)
        {
            string gyroflowPath = DialogHelper.ChooseFile("Select Gyroflow Executable", "Executable|*.exe", SettingsHandler.Settings.GyroflowPath, "gyroflow.exe");
            if (gyroflowPath.IsNullOrEmpty())
                return;
            SettingsHandler.Settings.GyroflowPath = gyroflowPath;
            TxCurrentGyroflowPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowPath ?? "Discoved automatically");
        }

        private void BtnDiscoverGyroflowExe_Click(object sender, RoutedEventArgs e)
        {
            //Auto discover Gyroflow executable path
            SettingsHandler.Settings.GyroflowPath = null;
            TxCurrentGyroflowPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowPath ?? "Discoved automatically");
            TxCurrentGyroflowSettingsPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowSettingsPath ?? "Using Default");
            Logger.LogInfo("Gyroflow path will be discovered automatically when needed.");
        }

        private void BtnPickGyroflowSettings_Click(object sender, RoutedEventArgs e)
        {
            string gyroflowSettingsPath = DialogHelper.ChooseFile("Select Gyroflow Settings", "Gyroflow Settings|*.gyroflow", SettingsHandler.Settings.GyroflowSettingsPath, "default.gyroflow");
            if (gyroflowSettingsPath.IsNullOrEmpty())
                return;
            SettingsHandler.Settings.GyroflowSettingsPath = gyroflowSettingsPath;
            TxCurrentGyroflowSettingsPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowSettingsPath ?? "Using Default");
        }

        private void BtnResetGyroflowSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsHandler.Settings.GyroflowSettingsPath = null;
            TxCurrentGyroflowSettingsPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowSettingsPath ?? "Using Default");
            Logger.LogInfo("Gyroflow settings path reset to default.");
        }

        private void BtnResetLayout_Click(object sender, RoutedEventArgs e)
        {
            var msg = MessageBox.Show("Are you sure you want to reset the layout? This will close the application. This will reset the positions and sizes of all windows to their default values.", "Reset Layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (msg == MessageBoxResult.Yes)
            {
                LayoutManager.DeleteLayoutFile();
                Logger.LogSuccess("Layout reset to default. Please restart the application to apply changes.");
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
