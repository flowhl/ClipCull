using System.Windows;
using ClipCull.Models.Import;

namespace ClipCull.Controls
{
    /// <summary>
    /// Final import confirmation: choose Move vs Copy and the post-import actions.
    /// </summary>
    public partial class ImportConfirmDialog : Window
    {
        public ImportOperation Operation { get; private set; }
        public bool OpenExplorerAfter => OpenExplorerCheck.IsChecked == true;
        public bool OpenEditorAfter => OpenEditorCheck.IsChecked == true;

        public ImportConfirmDialog(
            int importCount,
            int skipCount,
            ImportMediaKind mediaKind,
            ImportOperation defaultOperation,
            bool defaultOpenExplorer,
            bool defaultOpenEditor,
            bool reorganize = false)
        {
            InitializeComponent();

            if (reorganize)
            {
                Title = "Confirm Reorganize";
                SummaryLabel.Text = skipCount > 0
                    ? $"{importCount} file(s) will be moved into subfolders, {skipCount} already in place."
                    : $"{importCount} file(s) will be moved into subfolders.";

                // Reorganizing in place is always a move; copying would duplicate the library.
                Operation = ImportOperation.Move;
                CopyToggle.IsEnabled = false;
            }
            else
            {
                SummaryLabel.Text = skipCount > 0
                    ? $"{importCount} file(s) will be imported, {skipCount} skipped."
                    : $"{importCount} file(s) will be imported.";

                Operation = defaultOperation;
            }

            UpdateOperationToggles();

            OpenExplorerCheck.IsChecked = defaultOpenExplorer;

            // "Open in ClipCull editor" only makes sense for video imports.
            if (mediaKind == ImportMediaKind.Photo)
                OpenEditorCheck.Visibility = Visibility.Collapsed;
            else
                OpenEditorCheck.IsChecked = defaultOpenEditor;
        }

        private void CopyToggle_Click(object sender, RoutedEventArgs e)
        {
            Operation = ImportOperation.Copy;
            UpdateOperationToggles();
        }

        private void MoveToggle_Click(object sender, RoutedEventArgs e)
        {
            Operation = ImportOperation.Move;
            UpdateOperationToggles();
        }

        private void UpdateOperationToggles()
        {
            CopyToggle.IsChecked = Operation == ImportOperation.Copy;
            MoveToggle.IsChecked = Operation == ImportOperation.Move;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
