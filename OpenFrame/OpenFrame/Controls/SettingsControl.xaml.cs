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
            TxCurrentGyroflowPath.Text = "Path: " + (SettingsHandler.Settings.GyroflowPath ?? "Discoved automatically");
            Logger.LogInfo("Gyroflow path will be discovered automatically when needed.");
        }
    }
}
