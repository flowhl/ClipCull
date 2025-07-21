using ClipCull.Core;
using ClipCull.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClipCull.Controls
{
    public partial class FolderTreeControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private string _rootPath;
        private string _selectedPath;
        private bool _showFiles = false;
        private string _fileFilter = "*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v";
        private bool _isLoading = false;
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

        public string SelectedPath
        {
            get => _selectedPath;
            private set
            {
                if (_selectedPath != value)
                {
                    _selectedPath = value;
                    OnPropertyChanged(nameof(SelectedPath));
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
                    var currentSelectedPath = SelectedPath; // Remember current selection
                    _showFiles = value;
                    OnPropertyChanged(nameof(ShowFiles));
                    RefreshCurrentFolder();

                    // Try to restore selection after refresh
                    if (!string.IsNullOrEmpty(currentSelectedPath))
                    {
                        Task.Run(async () =>
                        {
                            await Task.Delay(500); // Give time for refresh to complete
                            await Dispatcher.InvokeAsync(async () => await ExpandToPathAsync(currentSelectedPath));
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
        public FolderTreeControl(string rootPath = null)
        {
            InitializeComponent();

            if (rootPath.IsNotNullOrEmpty())
                RootPath = rootPath;

            DataContext = this;

            this.PreviewMouseWheel += FolderTreeControl_PreviewMouseWheel;
            HotkeyController.OnReload += HotkeyController_OnReload;
            HotkeyController.OnEnter += HotkeyController_OnEnter;

            LoadTreeAsync();
        }
        #endregion

        private void FolderTreeControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ScrollViewerMain == null) return;

            ScrollViewerMain.ScrollToVerticalOffset(ScrollViewerMain.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        #region Public Methods
        public void LoadFolder(string rootPath)
        {
            RootPath = rootPath;
        }

        public async void JumpToFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                // Normalize the path
                path = Path.GetFullPath(path);

                // If no root path is set (showing drives), we need to find the correct drive first
                if (string.IsNullOrEmpty(RootPath))
                {
                    await JumpToFolderFromDrives(path);
                }
                else
                {
                    // If we have a root path, check if the target path is within it
                    if (path.StartsWith(RootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        await ExpandToPathAsync(path);
                    }
                    else
                    {
                        UpdateStatus($"Path {path} is not within root path {RootPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error jumping to folder: {ex.Message}");
            }
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
            if (string.IsNullOrEmpty(path))
                return;

            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        ExpandToPathRecursive(FolderTreeView.Items, path);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error expanding to path: {ex.Message}");
                    }
                });
            });
        }

        public void CollapseAll()
        {
            CollapseAllRecursive(FolderTreeView.Items);
        }

        public void ExpandAll()
        {
            ExpandAllRecursive(FolderTreeView.Items);
        }

        public string GetSelectedPath()
        {
            return SelectedPath;
        }
        #endregion

        #region Private Methods - Tree Loading
        private async void LoadTreeAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            FolderTreeView.Items.Clear();

            try
            {
                if (string.IsNullOrEmpty(RootPath))
                {
                    await LoadAllDrivesAsync();
                }
                else
                {
                    await LoadFolderAsync(RootPath);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading tree: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAllDrivesAsync()
        {
            await Task.Run(() =>
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady || d.DriveType == DriveType.Network)
                    .ToList();

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

                    UpdateStatus($"Loaded {drives.Count} drives");
                });
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
                        Dispatcher.Invoke(() =>
                        {
                            var rootItem = CreateFolderTreeItem(new DirectoryInfo(folderPath));

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
                HeaderTemplate = (DataTemplate)Resources["DriveItemTemplate"],
                Tag = driveData
            };

            // Always add dummy item for drives (they can potentially have subdirectories)
            item.Items.Add(new TreeViewItem { Header = "Loading..." });
            item.Expanded += TreeViewItem_Expanded;

            return item;
        }

        private TreeViewItem CreateFolderTreeItem(DirectoryInfo directory)
        {
            var folderData = new FolderItemData
            {
                Directory = directory,
                Name = directory.Name,
                FullPath = directory.FullName,
                IsFile = false
            };

            var item = new TreeViewItem
            {
                Header = folderData,
                HeaderTemplate = (DataTemplate)Resources["FolderItemTemplate"],
                Tag = folderData
            };

            // Always add dummy item for folders (they can potentially have subdirectories)
            item.Items.Add(new TreeViewItem { Header = "Loading..." });
            item.Expanded += TreeViewItem_Expanded;

            return item;
        }

        private TreeViewItem CreateFileTreeItem(FileInfo file)
        {
            var sidecarContent = SidecarService.GetSidecarContent(file.FullName);
            var fileData = new FileItemData
            {
                File = file,
                Name = file.Name,
                FullPath = file.FullName,
                SizeDisplay = GetFileSizeDisplay(file.Length),
                IsVideoFile = IsVideoFile(file.Extension),
                HasSidecar = IsVideoFile(file.Extension) && SidecarService.HasSidecar(file.FullName),
                IsPicked = sidecarContent?.UserMetadata?.Pick == true,
                IsReject = sidecarContent?.UserMetadata?.Pick == false,
            };

            var item = new TreeViewItem
            {
                Header = fileData,
                HeaderTemplate = (DataTemplate)Resources["FileItemTemplate"],
                Tag = fileData
            };

            return item;
        }

        private void HotkeyController_OnReload()
        {
            if (!IsVisible) return;
            RefreshTree();
        }

        private void HotkeyController_OnEnter()
        {
            if (!IsVisible) return;

            if (FolderTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                string path = GetItemPath(selectedItem);

                if (selectedItem.Tag is DriveItemData || selectedItem.Tag is FolderItemData)
                {
                    selectedItem.IsExpanded = !selectedItem.IsExpanded;
                }
                else if (selectedItem.Tag is FileItemData)
                {
                    FileDoubleClick?.Invoke(this, new FileDoubleClickEventArgs(path));
                }
            }
        }
        #endregion

        #region Private Methods - Lazy Loading
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;

            // Debug information
            Trace.WriteLine($"Expanding item. Items count: {item?.Items.Count ?? 0}");

            if (item?.Items.Count == 1)
            {
                var firstItem = item.Items[0];
                var headerText = firstItem is TreeViewItem tvi ? (tvi.Header?.ToString() ?? "null") : "Not TreeViewItem";
                Trace.WriteLine($"First item type: {firstItem?.GetType().Name ?? "null"}, Header: {headerText}");

                if (firstItem is TreeViewItem dummyItem &&
                    dummyItem.Header?.ToString() == "Loading...")
                {
                    Trace.WriteLine("Found Loading dummy item, clearing and loading children...");
                    item.Items.Clear();
                    LoadChildItems(item);
                }
                else
                {
                    Trace.WriteLine("No Loading dummy item found");
                }
            }
            else
            {
                Trace.WriteLine($"Item has {item?.Items.Count ?? 0} items, not expanding");
            }

            // Prevent the event from bubbling up to parent items
            e.Handled = true;
        }

        private async void LoadChildItems(TreeViewItem parentItem)
        {
            try
            {
                string parentPath = GetItemPath(parentItem);
                if (string.IsNullOrEmpty(parentPath) || !Directory.Exists(parentPath))
                {
                    Trace.WriteLine($"Path not accessible: {parentPath ?? "null"}");
                    UpdateStatus($"Path not accessible: {parentPath ?? "null"}");
                    return;
                }

                var folderName = Path.GetFileName(parentPath) ?? parentPath;
                Trace.WriteLine($"Loading contents of: {folderName}");
                UpdateStatus($"Loading contents of: {folderName}");

                await Task.Run(() =>
                {
                    try
                    {
                        // Get directories
                        var directories = Directory.GetDirectories(parentPath)
                            .Select(d => new DirectoryInfo(d))
                            .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) &&
                                       !d.Attributes.HasFlag(FileAttributes.System))
                            .OrderBy(d => d.Name)
                            .ToList();

                        // Get files if enabled
                        var files = ShowFiles ? GetFilteredFiles(parentPath).ToList() : new List<FileInfo>();

                        Dispatcher.Invoke(() =>
                        {
                            // Add subdirectories
                            foreach (var directory in directories)
                            {
                                try
                                {
                                    var childItem = CreateFolderTreeItem(directory);
                                    parentItem.Items.Add(childItem);
                                }
                                catch (Exception ex)
                                {
                                    // Skip inaccessible directories
                                    Trace.WriteLine($"Skipping directory {directory.Name}: {ex.Message}");
                                }
                            }

                            // Add files if enabled
                            foreach (var file in files)
                            {
                                try
                                {
                                    var fileItem = CreateFileTreeItem(file);
                                    parentItem.Items.Add(fileItem);
                                }
                                catch (Exception ex)
                                {
                                    // Skip inaccessible files
                                    Trace.WriteLine($"Skipping file {file.Name}: {ex.Message}");
                                }
                            }

                            var totalItems = directories.Count + files.Count;
                            Trace.WriteLine($"Loaded {directories.Count} folders, {files.Count} files");
                            UpdateStatus($"Loaded {directories.Count} folders, {files.Count} files");

                            // If no items were added, add a "No items" placeholder
                            if (totalItems == 0)
                            {
                                var emptyItem = new TreeViewItem
                                {
                                    Header = "(No items)",
                                    IsEnabled = false
                                };
                                parentItem.Items.Add(emptyItem);
                            }
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        var folderName = Path.GetFileName(parentPath) ?? parentPath;
                        Trace.WriteLine($"Access denied: {folderName}");
                        Dispatcher.Invoke(() => UpdateStatus($"Access denied: {folderName}"));
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error loading items: {ex.Message}");
                        Dispatcher.Invoke(() => UpdateStatus($"Error loading items: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error in LoadChildItems: {ex.Message}");
                UpdateStatus($"Error in LoadChildItems: {ex.Message}");
            }
        }

        private FileInfo[] GetFilteredFiles(string directoryPath)
        {
            try
            {
                var directory = new DirectoryInfo(directoryPath);
                var extensions = FileFilter.Split(';')
                    .Select(f => f.Trim().Replace("*", ""))
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToArray();

                return directory.GetFiles()
                    .Where(f => extensions.Any(ext => f.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(f => f.Name)
                    .ToArray();
            }
            catch
            {
                return new FileInfo[0];
            }
        }
        #endregion

        #region Private Methods - Helpers
        private string GetItemPath(TreeViewItem item)
        {
            if (item?.Tag == null)
                return string.Empty;

            if (item.Tag is DriveItemData driveData)
                return driveData.Drive.RootDirectory.FullName;
            else if (item.Tag is FolderItemData folderData)
                return folderData.FullPath;
            else if (item.Tag is FileItemData fileData)
                return fileData.FullPath;

            return string.Empty;
        }

        private bool HasSubDirectories(DirectoryInfo directory)
        {
            try
            {
                return directory.GetDirectories()
                    .Any(d => !d.Attributes.HasFlag(FileAttributes.Hidden));
            }
            catch
            {
                return false;
            }
        }

        private string GetDriveDisplayName(DriveInfo drive)
        {
            try
            {
                var label = !string.IsNullOrEmpty(drive.VolumeLabel) ? drive.VolumeLabel : "Local Disk";
                return $"{drive.Name.TrimEnd('\\')} ({label})";
            }
            catch
            {
                return drive.Name;
            }
        }

        private string GetDriveSpaceInfo(DriveInfo drive)
        {
            try
            {
                if (drive.IsReady)
                {
                    var freeSpace = GetFileSizeDisplay(drive.AvailableFreeSpace);
                    var totalSpace = GetFileSizeDisplay(drive.TotalSize);
                    return $"{freeSpace} free of {totalSpace}";
                }
            }
            catch
            {
                // Ignore errors
            }
            return string.Empty;
        }

        private string GetFileSizeDisplay(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private bool IsVideoFile(string extension)
        {
            var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };
            return videoExtensions.Contains(extension.ToLower());
        }

        private void ExpandToPathRecursive(ItemCollection items, string targetPath)
        {
            foreach (TreeViewItem item in items)
            {
                string itemPath = GetItemPath(item);
                if (!string.IsNullOrEmpty(itemPath) && targetPath.StartsWith(itemPath, StringComparison.OrdinalIgnoreCase))
                {
                    item.IsExpanded = true;

                    if (string.Equals(itemPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.IsSelected = true;
                        item.BringIntoView();
                        return;
                    }

                    if (item.Items.Count > 0)
                    {
                        ExpandToPathRecursive(item.Items, targetPath);
                    }
                }
            }
        }

        private void CollapseAllRecursive(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                item.IsExpanded = false;
                if (item.Items.Count > 0)
                {
                    CollapseAllRecursive(item.Items);
                }
            }
        }

        private void ExpandAllRecursive(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.Tag is DriveItemData || item.Tag is FolderItemData)
                {
                    item.IsExpanded = true;
                    if (item.Items.Count > 0)
                    {
                        ExpandAllRecursive(item.Items);
                    }
                }
            }
        }

        private void RefreshExpandedFolders(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.IsExpanded && (item.Tag is DriveItemData || item.Tag is FolderItemData))
                {
                    // Clear and reload this expanded folder
                    item.Items.Clear();
                    item.Items.Add(new TreeViewItem { Header = "Loading..." });
                    LoadChildItems(item);
                }
                else if (item.Items.Count > 0)
                {
                    RefreshExpandedFolders(item.Items);
                }
            }
        }

        private void UpdateStatus(string message = null)
        {
            if (IsLoading)
            {
                StatusText.Text = "Loading...";
            }
            else if (!string.IsNullOrEmpty(message))
            {
                StatusText.Text = message;
            }
            else
            {
                StatusText.Text = "Ready";
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Event Handlers
        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem)
            {
                string path = GetItemPath(selectedItem);
                SelectedPath = path;

                if (selectedItem.Tag is DriveItemData || selectedItem.Tag is FolderItemData)
                {
                    FolderSelected?.Invoke(this, new FolderSelectedEventArgs(path));
                    PathChanged?.Invoke(this, new PathChangedEventArgs(path, true));
                }
                else if (selectedItem.Tag is FileItemData)
                {
                    FileSelected?.Invoke(this, new FileSelectedEventArgs(path));
                    PathChanged?.Invoke(this, new PathChangedEventArgs(path, false));
                }

                UpdateStatus($"Selected: {Path.GetFileName(path) ?? path}");
            }
        }

        private void FolderTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FolderTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                string path = GetItemPath(selectedItem);

                if (selectedItem.Tag is DriveItemData || selectedItem.Tag is FolderItemData)
                {
                    FolderDoubleClick?.Invoke(this, new FolderDoubleClickEventArgs(path));
                }
                else if (selectedItem.Tag is FileItemData)
                {
                    FileDoubleClick?.Invoke(this, new FileDoubleClickEventArgs(path));
                }
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


        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = DialogHelper.ChooseFolder("Select Folder", RootPath);
            if (folder.IsNotNullOrEmpty())
            {
                LoadFolder(folder);
            }
        }

        private void ShowFilesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // ShowFiles property is already bound, this will trigger RefreshTree through the property setter
        }


        #region jump to folder

        private async Task JumpToFolderFromDrives(string targetPath)
        {
            await Task.Run(async () =>
            {
                try
                {
                    // Get the drive letter from the target path
                    string driveLetter = Path.GetPathRoot(targetPath);
                    if (string.IsNullOrEmpty(driveLetter))
                    {
                        Dispatcher.Invoke(() => UpdateStatus("Invalid path format"));
                        return;
                    }

                    await Dispatcher.InvokeAsync(async () =>
                    {
                        // Find the drive item in the tree
                        TreeViewItem driveItem = null;
                        foreach (TreeViewItem item in FolderTreeView.Items)
                        {
                            if (item.Tag is DriveItemData driveData)
                            {
                                if (driveData.Drive.RootDirectory.FullName.Equals(driveLetter, StringComparison.OrdinalIgnoreCase))
                                {
                                    driveItem = item;
                                    break;
                                }
                            }
                        }

                        if (driveItem == null)
                        {
                            UpdateStatus($"Drive {driveLetter} not found in tree");
                            return;
                        }

                        // Expand the drive if not already expanded
                        if (!driveItem.IsExpanded)
                        {
                            driveItem.IsExpanded = true;
                            // Wait for the drive to load its contents
                            await WaitForItemToLoad(driveItem);
                        }

                        // Now navigate through the folder structure
                        await NavigateToPathRecursive(driveItem, targetPath, driveLetter);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => UpdateStatus($"Error in JumpToFolderFromDrives: {ex.Message}"));
                }
            });
        }

        private async Task NavigateToPathRecursive(TreeViewItem currentItem, string targetPath, string currentPath)
        {
            try
            {
                // Remove trailing backslashes for comparison
                currentPath = currentPath.TrimEnd('\\');
                targetPath = targetPath.TrimEnd('\\');

                // If we've reached the target, select it
                if (string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Clear any existing selection first
                    ClearTreeViewSelection(FolderTreeView.Items);

                    // Select and focus the target item
                    currentItem.IsSelected = true;
                    currentItem.Focus();
                    currentItem.BringIntoView();

                    // Update the SelectedPath property
                    SelectedPath = targetPath;

                    // Scroll to make sure the item is visible
                    ScrollIntoView(currentItem);

                    UpdateStatus($"Navigated to: {targetPath}");
                    return;
                }

                // If target path doesn't start with current path, we're on wrong branch
                if (!targetPath.StartsWith(currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Get the next folder name in the path
                string remainingPath = targetPath.Substring(currentPath.Length).TrimStart('\\');
                string nextFolderName = remainingPath.Split('\\')[0];

                if (string.IsNullOrEmpty(nextFolderName))
                {
                    return;
                }

                // Expand current item if not already expanded
                if (!currentItem.IsExpanded)
                {
                    currentItem.IsExpanded = true;
                    await WaitForItemToLoad(currentItem);
                }

                // Find the next folder in the children
                foreach (TreeViewItem childItem in currentItem.Items)
                {
                    if (childItem.Tag is FolderItemData folderData)
                    {
                        if (string.Equals(folderData.Name, nextFolderName, StringComparison.OrdinalIgnoreCase))
                        {
                            string nextPath = Path.Combine(currentPath, nextFolderName);
                            await NavigateToPathRecursive(childItem, targetPath, nextPath);
                            return;
                        }
                    }
                    else if (childItem.Tag is FileItemData fileData)
                    {
                        // Check if we're looking for a file
                        if (string.Equals(fileData.Name, nextFolderName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Clear any existing selection first
                            ClearTreeViewSelection(FolderTreeView.Items);

                            // Select and focus the target file
                            childItem.IsSelected = true;
                            childItem.Focus();
                            childItem.BringIntoView();

                            // Update the SelectedPath property
                            SelectedPath = targetPath;

                            // Scroll to make sure the item is visible
                            ScrollIntoView(childItem);

                            UpdateStatus($"Navigated to file: {targetPath}");
                            return;
                        }
                    }
                }

                // If we get here, the folder/file wasn't found
                UpdateStatus($"Path not found: {Path.Combine(currentPath, nextFolderName)}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error navigating to path: {ex.Message}");
            }
        }

        private async Task WaitForItemToLoad(TreeViewItem item)
        {
            // Wait for the lazy loading to complete
            int maxWaitTime = 5000; // 5 seconds max wait
            int waitTime = 0;
            int checkInterval = 100; // Check every 100ms

            while (waitTime < maxWaitTime)
            {
                // Check if the item has been loaded (no longer has just a "Loading..." dummy item)
                if (item.Items.Count == 0 ||
                    (item.Items.Count == 1 &&
                     item.Items[0] is TreeViewItem dummyItem &&
                     dummyItem.Header?.ToString() == "Loading..."))
                {
                    await Task.Delay(checkInterval);
                    waitTime += checkInterval;
                }
                else
                {
                    // Item has been loaded
                    break;
                }
            }

            if (waitTime >= maxWaitTime)
            {
                UpdateStatus("Timeout waiting for folder to load");
            }
        }

        private void ClearTreeViewSelection(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                item.IsSelected = false;
                if (item.Items.Count > 0)
                {
                    ClearTreeViewSelection(item.Items);
                }
            }
        }

        private void ScrollIntoView(TreeViewItem item)
        {
            try
            {
                // Use the TreeView's built-in method to scroll the item into view
                item.BringIntoView();
                item.IsExpanded = true;

                // Also manually scroll the ScrollViewer if available
                if (ScrollViewerMain != null)
                {
                    // Get the item's position relative to the TreeView
                    var transform = item.TransformToAncestor(FolderTreeView);
                    var position = transform.Transform(new Point(0, 0));

                    // Calculate the center position
                    var targetOffset = position.Y - (ScrollViewerMain.ViewportHeight / 2);

                    // Ensure we don't scroll past the bounds
                    targetOffset = Math.Max(0, Math.Min(targetOffset, ScrollViewerMain.ScrollableHeight));

                    // Animate the scroll
                    ScrollViewerMain.ScrollToVerticalOffset(targetOffset);
                }
            }
            catch (Exception ex)
            {
                // If scrolling fails, just log it but don't crash
                UpdateStatus($"Warning: Could not scroll to item: {ex.Message}");
            }
        }
        #endregion

        #endregion

    }

    #region Data Classes
    public class DriveItemData
    {
        public DriveInfo Drive { get; set; }
        public string DisplayName { get; set; }
        public string SpaceInfo { get; set; }
        public string DriveType { get; set; }
        public bool IsFile { get; set; } = false;
    }

    public class FolderItemData
    {
        public DirectoryInfo Directory { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsFile { get; set; } = false;
    }

    public class FileItemData
    {
        public FileInfo File { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string SizeDisplay { get; set; }
        public bool IsVideoFile { get; set; }
        public bool IsFile { get; set; } = true;
        public bool HasSidecar { get; set; }
        public bool IsPicked{ get; set; }
        public bool IsReject { get; set; }
    }
    #endregion

    #region Event Args Classes
    public class FolderSelectedEventArgs : EventArgs
    {
        public string FolderPath { get; }

        public FolderSelectedEventArgs(string folderPath)
        {
            FolderPath = folderPath;
        }
    }

    public class FileSelectedEventArgs : EventArgs
    {
        public string FilePath { get; }

        public FileSelectedEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }

    public class PathChangedEventArgs : EventArgs
    {
        public string Path { get; }
        public bool IsFolder { get; }

        public PathChangedEventArgs(string path, bool isFolder)
        {
            Path = path;
            IsFolder = isFolder;
        }
    }

    public class FolderDoubleClickEventArgs : EventArgs
    {
        public string FolderPath { get; }

        public FolderDoubleClickEventArgs(string folderPath)
        {
            FolderPath = folderPath;
        }
    }

    public class FileDoubleClickEventArgs : EventArgs
    {
        public string FilePath { get; }

        public FileDoubleClickEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }
    #endregion
}