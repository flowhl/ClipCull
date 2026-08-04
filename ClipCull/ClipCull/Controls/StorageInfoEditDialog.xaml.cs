using System.IO;
using System.Windows;
using ClipCull.Core;
using ClipCull.Core.Import;
using ClipCull.Models.Import;

namespace ClipCull.Controls
{
    /// <summary>
    /// Editor for a storage medium's <c>info.xml</c> (camera model, reel, author, location).
    /// </summary>
    public partial class StorageInfoEditDialog : Window
    {
        public StorageInfoEditDialog(string initialFolder = null)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
            {
                FolderTextBox.Text = initialFolder;
                LoadExisting(initialFolder);
            }
        }

        private void LoadExisting(string folder)
        {
            var info = StorageInfoService.Read(folder);
            if (info == null) return;

            CameraTextBox.Text = info.CameraModel;
            ReelTextBox.Text = info.Reel;
            AuthorTextBox.Text = info.Author;
            LocationTextBox.Text = info.Location;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var folder = DialogHelper.ChooseFolder("Select the folder / card root", FolderTextBox.Text);
            if (string.IsNullOrEmpty(folder)) return;

            FolderTextBox.Text = folder;
            LoadExisting(folder);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var folder = FolderTextBox.Text;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("Please choose a valid folder first.", "Storage Info",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var info = new StorageInfo
            {
                CameraModel = CameraTextBox.Text?.Trim(),
                Reel = ReelTextBox.Text?.Trim(),
                Author = AuthorTextBox.Text?.Trim(),
                Location = LocationTextBox.Text?.Trim()
            };

            try
            {
                StorageInfoService.Write(folder, info);
                Logger.LogSuccess($"Saved storage info to {StorageInfoService.GetPath(folder)}", "Storage Info");
                DialogResult = true;
                Close();
            }
            catch
            {
                // Logger already surfaced the error.
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
