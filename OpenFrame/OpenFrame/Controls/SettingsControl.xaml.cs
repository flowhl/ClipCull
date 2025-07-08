using OpenFrame.Core;
using OpenFrame.Extensions;
using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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

namespace OpenFrame.Controls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public SettingsControl()
        {
            InitializeComponent();
            Loaded += SettingsControl_Loaded;
        }

        private void SettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsHandler.Load();
            DataContext = this;
            TxCurrentGyroflowPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowPath ?? "Discoved automatically");
            TxCurrentGyroflowSettingsPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowSettingsPath ?? "Using Default");

            var tagCollection = new ObservableCollection<EditableTag>();
            SettingsHandler.Settings.Tags.ForEach(x => tagCollection.Add(new EditableTag
            {
                Color = x.Color,
                Name = x.Name
            }));
            TagManagement.Tags = tagCollection;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var newTags = new List<Tag>();
            TagManagement.Tags.ToList().ForEach(x => newTags.Add(new Models.Tag
            {
                Color = x.Color,
                Name = x.Name
            }));

            SettingsHandler.Settings.Tags = newTags;

            SettingsHandler.Save();
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
    }
}
