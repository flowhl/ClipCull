using ClipCull.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core
{
    public static class DialogHelper
    {
        public static string ChooseFolder(string title, string initialfolder = null)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = title;
                if (initialfolder.IsNotNullOrEmpty())
                    dialog.SelectedPath = initialfolder;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && dialog.SelectedPath.IsNotNullOrEmpty())
                {
                    return dialog.SelectedPath;
                }
            }
            return null;
        }

        public static string ChooseFile(string title, string filter = null, string initialFolder = null, string fileName = null)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                if (filter.IsNotNullOrEmpty())
                    dialog.Filter = filter;
                if (initialFolder.IsNotNullOrEmpty())
                    dialog.InitialDirectory = initialFolder;
                if (fileName.IsNotNullOrEmpty())
                    dialog.FileName = fileName;
                dialog.Title = title;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && dialog.FileName.IsNotNullOrEmpty())
                {
                    return dialog.FileName;
                }
            }
            return null;
        }

        public static string SaveFile(string title, string extension, string initialFolder, string fileName)
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Filter = extension;
                dialog.InitialDirectory = initialFolder;
                dialog.FileName = fileName;
                dialog.Title = title;

                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && dialog.FileName.IsNotNullOrEmpty())
                {
                    return dialog.FileName;
                }
            }
            return null;
        }
    }
}
