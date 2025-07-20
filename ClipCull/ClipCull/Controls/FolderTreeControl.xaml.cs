using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Data;
using System.Globalization;
using ClipCull.Core;
using ClipCull.Models;

namespace ClipCull.Controls
{
    public partial class FolderTreeControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private string _rootPath;
        private bool _showFiles = false;
        private string _fileFilter = "*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v";
        private bool _isLoading = false;
        private string _statusText = "Ready";
        private bool _includeSubfolders = false;
        private string _currentSelectedPath;
        #endregion

        #region Properties
        public string RootPath
        {
            get => _rootPath;
            set
            {
                if (_rootPath != value)
                {
                    _rootPath = value;
                    OnPropertyChanged(nameof(RootPath));
                    LoadTreeAsync();
                }
            }
        }

        public bool ShowFiles
        {
            get => _showFiles;
            set
            {
                if (_showFiles != value)
                {
                    _showFiles = value;
                    OnPropertyChanged(nameof(ShowFiles));
                    RefreshTree();
                }
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (_includeSubfolders != value)
                {
                    _includeSubfolders = value;
                    OnPropertyChanged(nameof(IncludeSubfolders));
                    if (_currentSelectedPath != null)
                    {
                        // Refresh the current selection if we're showing a folder
                        Task.Run(async () =>
                        {
                            await Dispatcher.InvokeAsync(async () => await ExpandToPathAsync(_currentSelectedPath));
                        });
                    }
                }
            }
        }

        public string FileFilter
        {
            get => _fileFilter;
            set
            {
                if (_fileFilter != value)
                {
                    _fileFilter = value;
                    OnPropertyChanged(nameof(FileFilter));
                    if (ShowFiles)
                        RefreshTree();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                    UpdateStatus();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }
        #endregion

        #region Events
        public event EventHandler<FolderSelectedEventArgs> FolderSelected;
        public event EventHandler<FileSelectedEventArgs> FileSelected;
        public event EventHandler<PathChangedEventArgs> PathChanged;
        public event EventHandler<FolderDoubleClickEventArgs> FolderDoubleClick;
        public event EventHandler<FileDoubleClickEventArgs> FileDoubleClick;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Constructor
        public FolderTreeControl()
        {
            InitializeComponent();
            DataContext = this;

            // Load drives on startup if no root path is specified
            if (string.IsNullOrEmpty(RootPath))
            {
                LoadTreeAsync();
            }
        }
        #endregion

        #region Public Methods
        public void LoadFolder(string rootPath)
        {
            RootPath = rootPath;
        }

        public void RefreshTree()
        {
            LoadTreeAsync();
        }

        public void RefreshCurrentFolder()
        {
            RefreshExpandedFolders(FolderTreeView.Items);
        }

        public async Task ExpandToPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            try
            {
                IsLoading = true;

                var pathParts = GetPathParts(path);
                TreeViewItem currentItem = null;

                foreach (var part in pathParts)
                {
                    if (currentItem == null)
                    {
                        // Find root item (drive)
                        currentItem = FindDriveItem(part);
                    }
                    else
                    {
                        // Find child folder
                        if (!currentItem.IsExpanded)
                        {
                            currentItem.IsExpanded = true;
                            await LoadFolderContentsAsync(currentItem);
                        }
                        currentItem = FindChildItem(currentItem, part);
                    }

                    if (currentItem == null)
                        break;
                }

                if (currentItem != null)
                {
                    currentItem.IsSelected = true;
                    currentItem.BringIntoView();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void CollapseAll()
        {
            CollapseItems(FolderTreeView.Items);
        }

        public void ExpandAll()
        {
            ExpandItems(FolderTreeView.Items);
        }
        #endregion

        #region Private Methods - Tree Loading
        private async Task LoadTreeAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        IsLoading = true;
                        FolderTreeView.Items.Clear();
                    });

                    if (!string.IsNullOrEmpty(RootPath))
                    {
                        await LoadFolderAsync(RootPath);
                    }
                    else
                    {
                        await LoadDrivesAsync();
                    }
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateStatus($"Error loading tree: {ex.Message}");
                    });
                }
                finally
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        IsLoading = false;
                    });
                }
            });
        }

        private async Task LoadDrivesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady);

                    Dispatcher.Invoke(() =>
                    {
                        foreach (var drive in drives)
                        {
                            try
                            {
                                var driveItem = CreateDriveTreeItem(drive);
                                FolderTreeView.Items.Add(driveItem);
                            }
                            catch (Exception ex)
                            {
                                // Skip inaccessible drives
                                UpdateStatus($"Skipping drive {drive.Name}: {ex.Message}");
                            }
                        }

                        UpdateStatus($"Loaded {drives.Count()} drives");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus($"Error loading drives: {ex.Message}");
                    });
                }
            });
        }

        private async Task LoadFolderAsync(string folderPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(folderPath))
                    {
                        var rootItem = CreateFolderTreeItem(new DirectoryInfo(folderPath));

                        Dispatcher.Invoke(() =>
                        {
                            FolderTreeView.Items.Add(rootItem);
                            rootItem.IsExpanded = true;
                            UpdateStatus($"Loaded folder: {folderPath}");
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateStatus($"Folder not found: {folderPath}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus($"Error loading folder: {ex.Message}");
                    });
                }
            });
        }

        private TreeViewItem CreateDriveTreeItem(DriveInfo drive)
        {
            var driveData = new DriveItemData
            {
                Drive = drive,
                DisplayName = GetDriveDisplayName(drive),
                SpaceInfo = GetDriveSpaceInfo(drive),
                DriveType = drive.DriveType.ToString(),
                IsFile = false
            };

            var item = new TreeViewItem
            {
                Header = driveData,
                Tag = driveData,
                HeaderTemplate = (DataTemplate)Resources["DriveItemTemplate"]
            };

            // Add dummy child to show expander
            item.Items.Add(new TreeViewItem { Header = "Loading..." });
            item.Expanded += TreeViewItem_Expanded;

            return item;
        }

        private TreeViewItem CreateFolderTreeItem(DirectoryInfo directory)
        {
            var folderData = new FolderItemData
            {
                DirectoryInfo = directory,
                Name = directory.Name,
                FullPath = directory.FullName,
                IsFile = false
            };

            var item = new TreeViewItem
            {
                Header = folderData,
                Tag = folderData,
                HeaderTemplate = (DataTemplate)Resources["FolderItemTemplate"]
            };

            // Add dummy child to show expander
            if (HasSubDirectories(directory))
            {
                item.Items.Add(new TreeViewItem { Header = "Loading..." });
                item.Expanded += TreeViewItem_Expanded;
            }

            return item;
        }

        private TreeViewItem CreateFileTreeItem(FileInfo file)
        {
            var fileData = new FileItemData
            {
                FileInfo = file,
                Name = file.Name,
                FullPath = file.FullName,
                Extension = file.Extension.ToLowerInvariant(),
                IsFile = true,
                HasSidecar = CheckForSidecar(file.FullName)
            };

            var item = new TreeViewItem
            {
                Header = fileData,
                Tag = fileData,
                HeaderTemplate = (DataTemplate)Resources["FileItemTemplate"]
            };

            return item;
        }

        private bool CheckForSidecar(string filePath)
        {
            try
            {
                string sidecarPath = Path.ChangeExtension(filePath, ".xml");
                return File.Exists(sidecarPath);
            }
            catch
            {
                return false;
            }
        }

        private async Task LoadFolderContentsAsync(TreeViewItem folderItem)
        {
            try
            {
                // Get the folder path on the UI thread
                string folderPath = null;
                await Dispatcher.InvokeAsync(() =>
                {
                    var folderData = folderItem.Tag as FolderItemData;
                    var driveData = folderItem.Tag as DriveItemData;

                    if (folderData != null)
                    {
                        folderPath = folderData.FullPath;
                    }
                    else if (driveData != null)
                    {
                        folderPath = driveData.Drive.RootDirectory.FullName;
                    }
                });

                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return;

                // Do file system operations on background thread
                var subDirectories = new List<DirectoryInfo>();
                var files = new List<FileInfo>();

                await Task.Run(() =>
                {
                    try
                    {
                        var directoryInfo = new DirectoryInfo(folderPath);

                        subDirectories = directoryInfo.GetDirectories()
                            .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                            .OrderBy(d => d.Name)
                            .ToList();

                        if (ShowFiles)
                        {
                            var extensions = _fileFilter.Split(';')
                                .Select(f => f.Replace("*", "").ToLowerInvariant())
                                .ToHashSet();

                            files = directoryInfo.GetFiles()
                                .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                .Where(f => extensions.Contains(f.Extension.ToLowerInvariant()))
                                .OrderBy(f => f.Name)
                                .ToList();
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Will be handled in the UI thread update
                    }
                    catch (Exception)
                    {
                        // Will be handled in the UI thread update
                    }
                });

                // Update UI on the UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // Remove dummy item
                        folderItem.Items.Clear();

                        // Add subdirectories
                        foreach (var subDir in subDirectories)
                        {
                            try
                            {
                                var subItem = CreateFolderTreeItem(subDir);
                                folderItem.Items.Add(subItem);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                // Skip inaccessible directories
                            }
                        }

                        // Add files if ShowFiles is enabled
                        foreach (var file in files)
                        {
                            try
                            {
                                var fileItem = CreateFileTreeItem(file);
                                folderItem.Items.Add(fileItem);
                            }
                            catch (Exception)
                            {
                                // Skip problematic files
                            }
                        }

                        // If no items were added and there was an error, show error message
                        if (folderItem.Items.Count == 0 && (subDirectories.Count == 0 && files.Count == 0))
                        {
                            var errorItem = new TreeViewItem { Header = "Access denied or empty folder" };
                            folderItem.Items.Add(errorItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Remove dummy item and show error
                        folderItem.Items.Clear();
                        var errorItem = new TreeViewItem { Header = $"Error: {ex.Message}" };
                        folderItem.Items.Add(errorItem);
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // Remove dummy item and show error
                    folderItem.Items.Clear();
                    var errorItem = new TreeViewItem { Header = $"Error: {ex.Message}" };
                    folderItem.Items.Add(errorItem);
                });
            }
        }

        private void RefreshExpandedFolders(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.IsExpanded)
                {
                    Task.Run(async () => await LoadFolderContentsAsync(item));
                    RefreshExpandedFolders(item.Items);
                }
            }
        }
        #endregion

        #region Private Methods - Utilities
        private string GetDriveDisplayName(DriveInfo drive)
        {
            string label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
            return $"{label} ({drive.Name.TrimEnd('\\')})";
        }

        private string GetDriveSpaceInfo(DriveInfo drive)
        {
            try
            {
                double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                return $"{freeGB:F1} GB free of {totalGB:F1} GB";
            }
            catch
            {
                return "Size unknown";
            }
        }

        private bool HasSubDirectories(DirectoryInfo directory)
        {
            try
            {
                return directory.GetDirectories()
                    .Any(d => (d.Attributes & FileAttributes.Hidden) == 0);
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetPathParts(string path)
        {
            var parts = new List<string>();
            var directoryInfo = new DirectoryInfo(path);

            while (directoryInfo != null)
            {
                if (directoryInfo.Parent == null)
                {
                    // This is the root (drive)
                    parts.Insert(0, directoryInfo.FullName);
                }
                else
                {
                    parts.Insert(0, directoryInfo.Name);
                }
                directoryInfo = directoryInfo.Parent;
            }

            return parts;
        }

        private TreeViewItem FindDriveItem(string drivePath)
        {
            foreach (TreeViewItem item in FolderTreeView.Items)
            {
                var driveData = item.Tag as DriveItemData;
                if (driveData?.Drive.RootDirectory.FullName.Equals(drivePath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return item;
                }
            }
            return null;
        }

        private TreeViewItem FindChildItem(TreeViewItem parent, string name)
        {
            foreach (TreeViewItem item in parent.Items)
            {
                var folderData = item.Tag as FolderItemData;
                if (folderData?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return item;
                }
            }
            return null;
        }

        private void CollapseItems(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                item.IsExpanded = false;
                CollapseItems(item.Items);
            }
        }

        private void ExpandItems(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                item.IsExpanded = true;
                ExpandItems(item.Items);
            }
        }

        private void UpdateStatus(string message = null)
        {
            if (message != null)
            {
                StatusText = message;
            }
            else
            {
                StatusText = IsLoading ? "Loading..." : "Ready";
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Event Handlers
        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item?.Items.Count == 1 && item.Items[0] is TreeViewItem firstChild && firstChild.Header.ToString() == "Loading...")
            {
                await LoadFolderContentsAsync(item);
            }
        }

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as TreeViewItem;
            if (selectedItem?.Tag is FolderItemData folderData)
            {
                _currentSelectedPath = folderData.FullPath;
                FolderSelected?.Invoke(this, new FolderSelectedEventArgs(folderData.FullPath));
                PathChanged?.Invoke(this, new PathChangedEventArgs(folderData.FullPath));
            }
            else if (selectedItem?.Tag is DriveItemData driveData)
            {
                _currentSelectedPath = driveData.Drive.RootDirectory.FullName;
                FolderSelected?.Invoke(this, new FolderSelectedEventArgs(driveData.Drive.RootDirectory.FullName));
                PathChanged?.Invoke(this, new PathChangedEventArgs(driveData.Drive.RootDirectory.FullName));
            }
            else if (selectedItem?.Tag is FileItemData fileData)
            {
                _currentSelectedPath = Path.GetDirectoryName(fileData.FullPath);
                FileSelected?.Invoke(this, new FileSelectedEventArgs(fileData.FullPath));
                PathChanged?.Invoke(this, new PathChangedEventArgs(fileData.FullPath));
            }
        }

        private void FolderTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedItem = FolderTreeView.SelectedItem as TreeViewItem;
            if (selectedItem?.Tag is FolderItemData folderData)
            {
                FolderDoubleClick?.Invoke(this, new FolderDoubleClickEventArgs(folderData.FullPath));
            }
            else if (selectedItem?.Tag is FileItemData fileData)
            {
                FileDoubleClick?.Invoke(this, new FileDoubleClickEventArgs(fileData.FullPath));
            }
        }

        private void FolderTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshTree();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                var selectedItem = FolderTreeView.SelectedItem as TreeViewItem;
                if (selectedItem?.Tag is FolderItemData folderData)
                {
                    FolderDoubleClick?.Invoke(this, new FolderDoubleClickEventArgs(folderData.FullPath));
                }
                else if (selectedItem?.Tag is FileItemData fileData)
                {
                    FileDoubleClick?.Invoke(this, new FileDoubleClickEventArgs(fileData.FullPath));
                }
                e.Handled = true;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshTree();
        }

        private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
        {
            ExpandAll();
        }

        private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
        {
            CollapseAll();
        }
        #endregion
    }

    #region Data Classes
    public class DriveItemData
    {
        public DriveInfo Drive { get; set; }
        public string DisplayName { get; set; }
        public string SpaceInfo { get; set; }
        public string DriveType { get; set; }
        public bool IsFile { get; set; }
    }

    public class FolderItemData
    {
        public DirectoryInfo DirectoryInfo { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFile { get; set; }
    }

    public class FileItemData
    {
        public FileInfo FileInfo { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Extension { get; set; }
        public bool IsFile { get; set; }
        public bool HasSidecar { get; set; }
    }
    #endregion

    #region Event Args
    public class FolderSelectedEventArgs : EventArgs
    {
        public string SelectedPath { get; }
        public FolderSelectedEventArgs(string selectedPath) => SelectedPath = selectedPath;
    }

    public class FileSelectedEventArgs : EventArgs
    {
        public string SelectedPath { get; }
        public FileSelectedEventArgs(string selectedPath) => SelectedPath = selectedPath;
    }

    public class PathChangedEventArgs : EventArgs
    {
        public string Path { get; }
        public PathChangedEventArgs(string path) => Path = path;
    }

    public class FolderDoubleClickEventArgs : EventArgs
    {
        public string FolderPath { get; }
        public FolderDoubleClickEventArgs(string folderPath) => FolderPath = folderPath;
    }

    public class FileDoubleClickEventArgs : EventArgs
    {
        public string FilePath { get; }
        public FileDoubleClickEventArgs(string filePath) => FilePath = filePath;
    }
    #endregion

    #region Converters
    public class NotNullToBoolConverter : IValueConverter
    {
        public static readonly NotNullToBoolConverter Instance = new NotNullToBoolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}