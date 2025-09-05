using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ClipCull.Core;
using ClipCull.Core.Gyroflow;
using ClipCull.Extensions;
using ClipCull.Models;
using ClipCull.Models.ClipCull.Models;
using static ClipCull.Core.Gyroflow.GyroflowSubclipExtractor;

namespace ClipCull.Controls
{
    /// <summary>
    /// UserControl for browsing video clips with sidecar files
    /// </summary>
    public partial class VideoClipBrowserControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private string _folderPath;
        private bool _seekSubdirectories = true;
        private bool _isLoading;
        private VideoClipInfo _selectedClip;
        private string _selectedFile;
        private string _statusText = "Ready";
        #endregion

        #region Filtering Fields
        private FilterCriteria _filterCriteria;
        private ObservableCollection<VideoClipInfo> _allVideoClips = new ObservableCollection<VideoClipInfo>();
        private ObservableCollection<VideoClipInfo> _filteredVideoClips = new ObservableCollection<VideoClipInfo>();
        #endregion

        /// <summary>
        /// Collection of video clips (filtered view)
        /// </summary>
        public ObservableCollection<VideoClipInfo> VideoClips
        {
            get => _filteredVideoClips;
            private set => SetProperty(ref _filteredVideoClips, value);
        }

        #region Properties

        /// <summary>
        /// The folder path to scan for video files
        /// </summary>
        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (SetProperty(ref _folderPath, value))
                {
                    // Start refresh asynchronously without blocking the UI
                    _ = RefreshClipsAsync();
                }
            }
        }

        /// <summary>
        /// Whether to search subdirectories
        /// </summary>
        public bool SeekSubdirectories
        {
            get => _seekSubdirectories;
            set
            {
                if (SetProperty(ref _seekSubdirectories, value))
                {
                    if (!string.IsNullOrEmpty(FolderPath))
                    {
                        // Start refresh asynchronously without blocking the UI
                        _ = RefreshClipsAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Currently selected clip
        /// </summary>
        public VideoClipInfo SelectedClip
        {
            get => _selectedClip;
            set
            {
                if (SetProperty(ref _selectedClip, value))
                {
                    SelectedFile = value?.VideoFilePath;
                    ClipSelectionChanged?.Invoke(this, new ClipSelectionChangedEventArgs(value, SelectedFile));
                }
            }
        }

        /// <summary>
        /// Currently selected video file path
        /// </summary>
        public string SelectedFile
        {
            get => _selectedFile;
            private set => SetProperty(ref _selectedFile, value);
        }

        /// <summary>
        /// Whether the control is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Status text for the status bar
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Number of clips loaded (filtered count)
        /// </summary>
        public int ClipCount => _filteredVideoClips?.Count ?? 0;

        /// <summary>
        /// Total number of clips (unfiltered)
        /// </summary>
        public int TotalClipCount => _allVideoClips?.Count ?? 0;

        /// <summary>
        /// Whether the clips collection is empty
        /// </summary>
        public bool IsEmpty => !IsLoading && ClipCount == 0;

        /// <summary>
        /// Number of selected clips
        /// </summary>
        public int SelectedClipCount => _filteredVideoClips?.Count(c => c.IsSelected) ?? 0;

        /// <summary>
        /// Collection of selected clips
        /// </summary>
        public IEnumerable<VideoClipInfo> SelectedClips => _filteredVideoClips?.Where(c => c.IsSelected) ?? Enumerable.Empty<VideoClipInfo>();

        #endregion

        #region Events

        /// <summary>
        /// Fired when clip selection changes
        /// </summary>
        public event EventHandler<ClipSelectionChangedEventArgs> ClipSelectionChanged;

        /// <summary>
        /// Fired when clip checkbox selection changes
        /// </summary>
        public event EventHandler<ClipCheckboxSelectionChangedEventArgs> ClipCheckboxSelectionChanged;

        #endregion

        #region Constructor

        public VideoClipBrowserControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        #endregion

        #region Public Methods

        #region Filtering Methods

        /// <summary>
        /// Apply filter criteria to the clips collection
        /// </summary>
        /// <param name="filterCriteria">The filter criteria to apply</param>
        public void ApplyFilter(FilterCriteria filterCriteria)
        {
            _filterCriteria = filterCriteria;
            UpdateFilteredView();
        }

        /// <summary>
        /// Update the filtered view based on current filter criteria
        /// </summary>
        private void UpdateFilteredView()
        {
            if (_filterCriteria == null || !_filterCriteria.IsActive)
            {
                // No filter active - show all clips
                UpdateFilteredClips(_allVideoClips);
                return;
            }

            // Apply filtering
            var filteredClips = _allVideoClips.Where(clip => MatchesFilter(clip)).ToList();
            UpdateFilteredClips(filteredClips);
        }

        /// <summary>
        /// Update the filtered clips collection on the UI thread
        /// </summary>
        private void UpdateFilteredClips(IEnumerable<VideoClipInfo> clips)
        {
            Dispatcher.Invoke(() =>
            {
                _filteredVideoClips.Clear();
                foreach (var clip in clips)
                {
                    _filteredVideoClips.Add(clip);
                }

                // Update related properties
                OnPropertyChanged(nameof(ClipCount));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(SelectedClipCount));

                // Update status text to show filtering
                if (_filterCriteria?.IsActive == true)
                {
                    var totalClips = _allVideoClips.Count;
                    var filteredCount = _filteredVideoClips.Count;
                    StatusText = $"Showing {filteredCount} of {totalClips} clips (filtered)";
                }
                else
                {
                    StatusText = $"Loaded {_filteredVideoClips.Count} clips";
                }
            });
        }

        #region Filtermatching
        /// <summary>
        /// Check if a clip matches the current filter criteria
        /// </summary>
        private bool MatchesFilter(VideoClipInfo clip)
        {
            // No filter means show everything
            if (_filterCriteria == null || !_filterCriteria.IsActive)
                return true;

            // Check clip title first (from VideoClipInfo itself)
            if (MatchesClipTitle(clip))
                return true;

            // Check SubClip data if present
            if (clip.SubClip != null && _filterCriteria.Matches(clip.SubClip))
                return true;

            // Check UserMetadata from sidecar
            var metadata = clip.UserMetadata;
            if (metadata != null && _filterCriteria.Matches(metadata))
                return true;

            // Handle clips with no metadata - show them only if no specific criteria are set
            if (metadata == null)
                return AllowEmptyMetadata();

            return false;
        }

        /// <summary>
        /// Check if clip title matches search criteria
        /// </summary>
        private bool MatchesClipTitle(VideoClipInfo clip)
        {
            if (string.IsNullOrWhiteSpace(_filterCriteria.SearchText))
                return false;

            if (clip.ClipTitle.IsNullOrEmpty())
                return false;

            var searchLower = _filterCriteria.SearchText.ToLowerInvariant();
            return clip.ClipTitle.ToLowerInvariant().Contains(searchLower);
        }

        /// <summary>
        /// Determine if clips without metadata should be shown
        /// Only show them if no specific criteria are active
        /// Tags are always exclusive - if filtering by tags, clips without metadata should not appear
        /// </summary>
        private bool AllowEmptyMetadata()
        {
            // If any tags are selected, don't show clips without metadata
            if ((_filterCriteria.SelectedTags?.Count ?? 0) > 0)
                return false;

            // For other criteria, only show if no specific criteria are active
            return _filterCriteria.PickStatus == null &&
                   _filterCriteria.MinRating == null &&
                   _filterCriteria.MaxRating == null &&
                   string.IsNullOrWhiteSpace(_filterCriteria.SearchText);
        }
        #endregion

        /// <summary>
        /// Get UserMetadata for a clip (handles both main clips and subclips)
        /// </summary>
        private UserMetadataContent GetClipUserMetadata(VideoClipInfo clip)
        {
            try
            {
                // For both main clips and subclips, we need to get the sidecar content
                // since that's where UserMetadata is stored
                var sidecarContent = SidecarService.GetSidecarContent(clip.VideoFilePath);
                return sidecarContent?.UserMetadata;
            }
            catch (Exception ex)
            {
                // Log error but don't break filtering
                Logger.LogError($"Error getting metadata for clip {clip.VideoFileName}: {ex.Message}", ex);
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Refresh the clips list asynchronously
        /// </summary>
        public async Task RefreshClipsAsync()
        {
            if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
            {
                // Update UI on the UI thread
                Dispatcher.Invoke(() =>
                {
                    VideoClips.Clear();
                    StatusText = "No folder selected";
                    OnPropertyChanged(nameof(ClipCount));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(SelectedClipCount));
                });
                return;
            }

            // Update loading state on UI thread
            Dispatcher.Invoke(() =>
            {
                IsLoading = true;
                StatusText = "Loading clips...";
                OnPropertyChanged(nameof(IsEmpty));
            });

            try
            {
                await LoadClipsFromFolder();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText = $"Error loading clips: {ex.Message}";
                });
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    IsLoading = false;
                    OnPropertyChanged(nameof(IsEmpty));
                });
            }
        }

        /// <summary>
        /// Refresh the clips list (synchronous wrapper for backward compatibility)
        /// </summary>
        public async void RefreshClips()
        {
            await RefreshClipsAsync();
        }

        /// <summary>
        /// Clear all clips
        /// </summary>
        public void ClearClips()
        {
            _allVideoClips.Clear();
            _filteredVideoClips.Clear();
            SelectedClip = null;
            StatusText = "Ready";
            OnPropertyChanged(nameof(ClipCount));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SelectedClipCount));
        }

        /// <summary>
        /// Select all clips
        /// </summary>
        public void SelectAllClips()
        {
            foreach (var clip in VideoClips)
            {
                clip.IsSelected = true;
            }
            OnPropertyChanged(nameof(SelectedClipCount));
            ClipCheckboxSelectionChanged?.Invoke(this, new ClipCheckboxSelectionChangedEventArgs(SelectedClips.ToList()));
        }

        /// <summary>
        /// Deselect all clips
        /// </summary>
        public void DeselectAllClips()
        {
            foreach (var clip in VideoClips)
            {
                clip.IsSelected = false;
            }
            OnPropertyChanged(nameof(SelectedClipCount));
            ClipCheckboxSelectionChanged?.Invoke(this, new ClipCheckboxSelectionChangedEventArgs(SelectedClips.ToList()));
        }
        #endregion

        #region Private Methods

        private async Task LoadClipsFromFolder()
        {
            var clips = new List<VideoClipInfo>();
            var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm" };

            try
            {
                var searchOption = SeekSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var videoFiles = Directory.GetFiles(FolderPath, "*.*", searchOption)
                    .Where(file => videoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();

                // Update status on UI thread
                Dispatcher.Invoke(() => StatusText = $"Found {videoFiles.Count} video files, processing...");

                var processedFiles = new Dictionary<string, List<VideoClipInfo>>();

                // Process files in background
                await Task.Run(() =>
                {
                    foreach (var videoFile in videoFiles)
                    {
                        try
                        {
                            var sidecarContent = SidecarService.GetSidecarContent(videoFile);
                            var fileClips = new List<VideoClipInfo>();

                            // Check if there's a main clip (InPoint/OutPoint)
                            if (sidecarContent?.InPoint != null && sidecarContent?.OutPoint != null)
                            {
                                var mainClipInfo = CreateMainClipInfo(videoFile, sidecarContent);
                                if (mainClipInfo != null)
                                {
                                    fileClips.Add(mainClipInfo);
                                }
                            }

                            // Add subclips if they exist
                            if (sidecarContent?.SubClips != null && sidecarContent.SubClips.Any())
                            {
                                foreach (var subClip in sidecarContent.SubClips)
                                {
                                    var clipInfo = new VideoClipInfo
                                    {
                                        VideoFilePath = videoFile,
                                        VideoFileName = Path.GetFileName(videoFile),
                                        SubClip = subClip,
                                        ClipTitle = !string.IsNullOrEmpty(subClip.Title) ?
                                            subClip.Title : $"Clip {subClip.Id}",
                                        StartTimeMs = subClip.StartTime,
                                        EndTimeMs = subClip.EndTime,
                                        ClipColor = subClip.Color,
                                        ClipType = ClipType.SubClip,
                                    };

                                    clipInfo.UserMetadata = GetClipUserMetadata(clipInfo);

                                    // Subscribe to property changes for UI updates
                                    clipInfo.PropertyChanged += ClipInfo_PropertyChanged;
                                    fileClips.Add(clipInfo);
                                }
                            }

                            if (fileClips.Any())
                            {
                                processedFiles[videoFile] = fileClips;
                                clips.AddRange(fileClips);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Error processing video file {videoFile}: {ex.Message}", ex);
                            // Continue with other files
                        }
                    }
                });

                // Update UI on UI thread
                Dispatcher.Invoke(() =>
                {
                    // Update the master collection
                    _allVideoClips.Clear();
                    foreach (var clip in clips)
                    {
                        _allVideoClips.Add(clip);
                    }

                    // Apply current filter (or show all if no filter)
                    UpdateFilteredView();
                });

                // Load thumbnails asynchronously without blocking
                _ = Task.Run(() => LoadThumbnailsAsync(clips));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText = $"Error loading clips: {ex.Message}";
                });
                throw;
            }
        }

        /// <summary>
        /// Creates a main clip info from InPoint/OutPoint data
        /// </summary>
        private VideoClipInfo CreateMainClipInfo(string videoFile, SidecarContent sidecarContent)
        {
            try
            {
                var startTime = Math.Min(sidecarContent.InPoint.Timestamp, sidecarContent.OutPoint.Timestamp);
                var endTime = Math.Max(sidecarContent.InPoint.Timestamp, sidecarContent.OutPoint.Timestamp);
                var duration = endTime - startTime;

                string titleIn = sidecarContent.InPoint.Title;
                string titleOut = sidecarContent.OutPoint.Title;
                string title = "Main Clip";
                if (titleIn.IsNotNullOrEmpty() && titleOut.IsNotNullOrEmpty())
                {
                    title = $"{titleIn} - {titleOut}";
                }
                else if (titleIn.IsNotNullOrEmpty())
                {
                    title = titleIn;
                }
                else if (titleOut.IsNotNullOrEmpty())
                {
                    title = titleOut;
                }

                var mainClipInfo = new VideoClipInfo
                {
                    VideoFilePath = videoFile,
                    VideoFileName = Path.GetFileName(videoFile),
                    SubClip = null, // Main clips don't have a SubClip object
                    ClipTitle = title,
                    StartTimeMs = startTime,
                    EndTimeMs = endTime,
                    ClipColor = System.Windows.Media.Colors.DodgerBlue, // Default color for main clips
                    ClipType = ClipType.MainClip,
                    IsSelected = false,
                    IsLoadingThumbnail = true,
                    UserMetadata = sidecarContent.UserMetadata
                };

                // Subscribe to selection changes
                mainClipInfo.PropertyChanged += ClipInfo_PropertyChanged;

                return mainClipInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating main clip info for {videoFile}: {ex.Message}");
                return null;
            }
        }

        private async Task LoadThumbnailsAsync(List<VideoClipInfo> clips)
        {
            await Task.Run(() =>
            {
                foreach (var clip in clips)
                {
                    try
                    {
                        var thumbnailPath = ThumbnailService.GetThumbnail(clip.VideoFilePath);

                        // Update UI on the UI thread
                        Dispatcher.Invoke(() =>
                        {
                            clip.ThumbnailPath = thumbnailPath;
                            clip.IsLoadingThumbnail = false;
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            clip.IsLoadingThumbnail = false;
                        });
                        System.Diagnostics.Debug.WriteLine($"Error loading thumbnail for {clip.VideoFilePath}: {ex.Message}");
                    }
                }
            });
        }

        #region Edit clips
        private void EditClip(VideoClipInfo info)
        {
            //Subclip
            if (info.IsSubClip)
            {
                EditSubclip(info.SubClip);
            }
            //Mainclip
            else if (info.IsMainClip)
            {
                EditMainClip(info);
            }
        }

        private void EditSubclip(SubClip subclip)
        {
            if (subclip == null)
            {
                Logger.LogWarning("Cannot edit null subclip.");
                return;
            }

            try
            {
                // Create and show the SubClip edit dialog, deletion disabled
                var dialog = new SubClipEditDialog(subclip, 0, allowDelete: false)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true)
                {
                    // SubClip object was modified in the dialog, now save to sidecar
                    SaveSubclipChangesToSidecar(subclip);

                    // Refresh the clips display to show the changes
                    _ = RefreshClipsAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error editing subclip '{subclip.Title}': {ex.Message}", ex);
                StatusText = $"Error editing subclip: {ex.Message}";
            }
        }

        private void EditMainClip(VideoClipInfo info)
        {
            if (info == null || !info.IsMainClip)
            {
                Logger.LogWarning("Cannot edit null or non-main clip.");
                return;
            }

            try
            {
                // Get the current sidecar content to access the InPoint and OutPoint
                var sidecarContent = SidecarService.GetSidecarContent(info.VideoFilePath);

                if (sidecarContent?.InPoint == null || sidecarContent?.OutPoint == null)
                {
                    Logger.LogError("Main clip does not have valid InPoint/OutPoint data.");
                    StatusText = "Error: Main clip missing timing data.";
                    return;
                }

                // For main clips, we need to edit both InPoint and OutPoint
                // You might want to create a combined dialog, or edit them separately
                // For now, I'll show how to edit the InPoint, then OutPoint

                // Edit InPoint first
                var inPointDialog = new ClipPointEditDialog(sidecarContent.InPoint, 0)
                {
                    Owner = Window.GetWindow(this)
                };

                bool inPointChanged = false;
                if (inPointDialog.ShowDialog() == true)
                {
                    if (inPointDialog.DeleteRequested)
                    {
                        // Handle InPoint deletion
                        sidecarContent.InPoint = null;
                        inPointChanged = true;
                    }
                    else
                    {
                        inPointChanged = true;
                    }
                }

                // Edit OutPoint if InPoint wasn't deleted
                bool outPointChanged = false;
                if (sidecarContent.InPoint != null)
                {
                    var outPointDialog = new ClipPointEditDialog(sidecarContent.OutPoint, 0)
                    {
                        Owner = Window.GetWindow(this)
                    };

                    if (outPointDialog.ShowDialog() == true)
                    {
                        if (outPointDialog.DeleteRequested)
                        {
                            // Handle OutPoint deletion
                            sidecarContent.OutPoint = null;
                            outPointChanged = true;
                        }
                        else
                        {
                            outPointChanged = true;
                        }
                    }
                }

                // Save changes if any were made
                if (inPointChanged || outPointChanged)
                {
                    SidecarService.SaveSidecarContent(sidecarContent, info.VideoFilePath);

                    // Refresh the clips display to show the changes
                    _ = RefreshClipsAsync();

                    StatusText = "Main clip updated successfully.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error editing main clip for '{info.VideoFileName}': {ex.Message}", ex);
                StatusText = $"Error editing main clip: {ex.Message}";
            }
        }

        #endregion

        private void ShowFileInExplorer(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    Logger.LogError($"File not found: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open explorer for {filePath}", ex);
            }
        }

        private void OpenFileWithDefaultProgram(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Logger.LogError($"File not found: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open file {filePath}", ex);
            }
        }

        private void AddSelectedClipsToRenderQueue()
        {
            var selectedClips = SelectedClips.ToList();
            if (selectedClips.Count == 0)
            {
                Logger.LogWarning("No clips selected for rendering.");
                return;
            }

            foreach (var clip in selectedClips)
            {
                TimeSpan startTime = TimeSpan.FromMilliseconds(clip.StartTimeMs);
                TimeSpan endTime = TimeSpan.FromMilliseconds(clip.EndTimeMs);

                //gyroflow rotates differently, 90° = 270, 180° = 180, 270° = 90, 0° = 0
                int translatedRotation = 0;
                if (SettingsHandler.Settings.GyroflowRenderWithRotation)
                {
                    switch (clip.UserMetadata.Rotation)
                    {
                        case 90:
                            translatedRotation = 270;
                            break;
                        case 180:
                            translatedRotation = 180;
                            break;
                        case 270:
                            translatedRotation = 90;
                            break;
                        default:
                            translatedRotation = 0;
                            break;
                    }
                }

                // output path with clean windows file
                string fileOutputName = $"{clip.ClipTitle}_{Path.GetFileNameWithoutExtension(clip.VideoFileName)}_subclip_{startTime:mm\\-ss}_{endTime:mm\\-ss}_stabilized.mp4";
                fileOutputName = fileOutputName.ToValidWindowsFileName();
                fileOutputName = fileOutputName.ToUrlSafeFilename(); // Further clean to be URL safe as gyroflow does not like url encoded chars

                var subclipInfo = new SubclipInfo()
                {
                    VideoFile = clip.VideoFilePath,
                    StartTime = TimeSpan.FromMilliseconds(clip.StartTimeMs),
                    EndTime = TimeSpan.FromMilliseconds(clip.EndTimeMs),
                    OutputName = fileOutputName,
                    Rotation = translatedRotation,
                };
                try
                {
                    // Add to render queue (assuming a method exists for this)
                    GyroFlowRenderQueue.Enqueue(subclipInfo);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to add clip {clip.ClipTitle} to render queue", ex);
                }
            }
        }

        #endregion

        #region Event Handlers

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshClipsAsync();
        }

        private void ClipsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is VideoClipInfo clip)
            {
                SelectedClip = clip;
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            SelectAllClips();
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            DeselectAllClips();
        }

        private void OpenWithDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if this is a button click from within a list item
            if (sender is System.Windows.Controls.Button button && button.Tag is VideoClipInfo clipInfo)
            {
                // Individual clip action
                OpenFileWithDefaultProgram(clipInfo.VideoFilePath);
            }
        }

        private void ShowInExplorerButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if this is a button click from within a list item
            if (sender is System.Windows.Controls.Button button && button.Tag is VideoClipInfo clipInfo)
            {
                // Individual clip action
                ShowFileInExplorer(clipInfo.VideoFilePath);
            }
        }

        private void EditSubclipButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if this is a button click from within a list item
            if (sender is System.Windows.Controls.Button button && button.Tag is VideoClipInfo clipInfo)
            {
                // Individual clip action
                EditClip(clipInfo);
            }
        }

        private void AddToRenderQueueButton_Click(object sender, RoutedEventArgs e)
        {
            AddSelectedClipsToRenderQueue();
        }

        private void ClipInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoClipInfo.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedClipCount));
                ClipCheckboxSelectionChanged?.Invoke(this, new ClipCheckboxSelectionChangedEventArgs(SelectedClips.ToList()));
            }
        }

        #endregion

        #region Helper Methods for Sidecar Operations

        /// <summary>
        /// Remove a subclip from the sidecar file
        /// </summary>
        private void RemoveSubclipFromSidecar(SubClip subclip)
        {
            try
            {
                // Find the video file that contains this subclip
                var videoFile = _allVideoClips.FirstOrDefault(c => c.SubClip == subclip)?.VideoFilePath;

                if (string.IsNullOrEmpty(videoFile))
                {
                    Logger.LogError("Could not find video file for subclip.");
                    return;
                }

                // Get current sidecar content
                var sidecarContent = SidecarService.GetSidecarContent(videoFile);

                if (sidecarContent?.SubClips != null)
                {
                    // Remove the subclip from the list
                    var subclipToRemove = sidecarContent.SubClips.FirstOrDefault(sc => sc.Id == subclip.Id);
                    if (subclipToRemove != null)
                    {
                        sidecarContent.SubClips.Remove(subclipToRemove);

                        // Save the updated sidecar content
                        SidecarService.SaveSidecarContent(sidecarContent, videoFile);

                        StatusText = $"Subclip '{subclip.Title}' deleted successfully.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error removing subclip from sidecar: {ex.Message}", ex);
                StatusText = $"Error deleting subclip: {ex.Message}";
            }
        }

        /// <summary>
        /// Save subclip changes to the sidecar file
        /// </summary>
        private void SaveSubclipChangesToSidecar(SubClip subclip)
        {
            try
            {
                // Find the video file that contains this subclip
                var videoFile = _allVideoClips.FirstOrDefault(c => c.SubClip == subclip)?.VideoFilePath;

                if (string.IsNullOrEmpty(videoFile))
                {
                    Logger.LogError("Could not find video file for subclip.");
                    return;
                }

                // Get current sidecar content
                var sidecarContent = SidecarService.GetSidecarContent(videoFile);

                if (sidecarContent?.SubClips != null)
                {
                    // Find and update the subclip in the sidecar
                    var existingSubclip = sidecarContent.SubClips.FirstOrDefault(sc => sc.Id == subclip.Id);
                    if (existingSubclip != null)
                    {
                        existingSubclip.Title = subclip.Title;
                        existingSubclip.StartTime = subclip.StartTime;
                        existingSubclip.EndTime = subclip.EndTime;
                        existingSubclip.Color = subclip.Color;

                        SidecarService.SaveSidecarContent(sidecarContent, videoFile);

                        StatusText = $"Subclip '{subclip.Title}' updated successfully.";
                    }
                    else
                    {
                        Logger.LogError("Could not find subclip in sidecar content.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving subclip changes: {ex.Message}", ex);
                StatusText = $"Error saving subclip changes: {ex.Message}";
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Information about a video clip
    /// </summary>
    public class VideoClipInfo : INotifyPropertyChanged
    {
        private string _thumbnailPath;
        private bool _isLoadingThumbnail;
        private bool _isSelected;
        private System.Windows.Media.SolidColorBrush _clipColorBrush;

        public UserMetadataContent UserMetadata { get; set; }

        public string VideoFilePath { get; set; }
        public string VideoFileName { get; set; }
        public SubClip SubClip { get; set; }
        public string ClipTitle { get; set; }
        public long StartTimeMs { get; set; }
        public long EndTimeMs { get; set; }
        public long DurationMs
        {
            get
            {
                return EndTimeMs - StartTimeMs;
            }
        }
        public string DurationString
        {
            get
            {
                return TimeSpan.FromMilliseconds(EndTimeMs - StartTimeMs).ToString(@"mm\:ss\.fff");
            }
        }
        public string StartTimeDisplay
        {
            get
            {
                return TimeSpan.FromMilliseconds(StartTimeMs).ToString(@"mm\:ss\.fff");
            }
        }
        public string EndTimeDisplay
        {
            get
            {
                return TimeSpan.FromMilliseconds(EndTimeMs).ToString(@"mm\:ss\.fff");
            }
        }
        public System.Windows.Media.Color ClipColor { get; set; }
        public bool IsFirstClipOfFile { get; set; }

        /// <summary>
        /// Type of clip (Main or Sub)
        /// </summary>
        public ClipType ClipType { get; set; }

        /// <summary>
        /// Whether this is a main clip (convenience property)
        /// </summary>
        public bool IsMainClip => ClipType == ClipType.MainClip;

        /// <summary>
        /// Whether this is a sub clip (convenience property)
        /// </summary>
        public bool IsSubClip => ClipType == ClipType.SubClip;

        /// <summary>
        /// Whether this clip is selected via checkbox
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public System.Windows.Media.SolidColorBrush ClipColorBrush
        {
            get
            {
                if (_clipColorBrush == null)
                {
                    _clipColorBrush = new System.Windows.Media.SolidColorBrush(ClipColor);
                    _clipColorBrush.Freeze(); // Make it thread-safe
                }
                return _clipColorBrush;
            }
        }

        public string ThumbnailPath
        {
            get => _thumbnailPath;
            set
            {
                if (_thumbnailPath != value)
                {
                    _thumbnailPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoadingThumbnail
        {
            get => _isLoadingThumbnail;
            set
            {
                if (_isLoadingThumbnail != value)
                {
                    _isLoadingThumbnail = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Event arguments for clip selection changed event
    /// </summary>
    public class ClipSelectionChangedEventArgs : EventArgs
    {
        public VideoClipInfo SelectedClip { get; }
        public string SelectedFile { get; }

        public ClipSelectionChangedEventArgs(VideoClipInfo selectedClip, string selectedFile)
        {
            SelectedClip = selectedClip;
            SelectedFile = selectedFile;
        }
    }

    /// <summary>
    /// Event arguments for clip checkbox selection changed event
    /// </summary>
    public class ClipCheckboxSelectionChangedEventArgs : EventArgs
    {
        public IReadOnlyList<VideoClipInfo> SelectedClips { get; }

        public ClipCheckboxSelectionChangedEventArgs(IReadOnlyList<VideoClipInfo> selectedClips)
        {
            SelectedClips = selectedClips;
        }
    }
}