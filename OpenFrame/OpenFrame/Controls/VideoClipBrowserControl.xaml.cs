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
using OpenFrame.Core;
using OpenFrame.Core.Gyroflow;
using OpenFrame.Models;
using static OpenFrame.Core.Gyroflow.GyroflowSubclipExtractor;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace OpenFrame.Controls
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
        private ObservableCollection<VideoClipInfo> _videoClips = new ObservableCollection<VideoClipInfo>();
        #endregion

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
        /// Collection of video clips
        /// </summary>
        public ObservableCollection<VideoClipInfo> VideoClips
        {
            get => _videoClips;
            private set => SetProperty(ref _videoClips, value);
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
        /// Number of clips loaded
        /// </summary>
        public int ClipCount => VideoClips?.Count ?? 0;

        /// <summary>
        /// Whether the clips collection is empty
        /// </summary>
        public bool IsEmpty => !IsLoading && ClipCount == 0;

        /// <summary>
        /// Number of selected clips
        /// </summary>
        public int SelectedClipCount => VideoClips?.Count(c => c.IsSelected) ?? 0;

        /// <summary>
        /// Collection of selected clips
        /// </summary>
        public IEnumerable<VideoClipInfo> SelectedClips => VideoClips?.Where(c => c.IsSelected) ?? Enumerable.Empty<VideoClipInfo>();

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
            VideoClips.Clear();
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
                                            subClip.Title : $"Clip {subClip.Id.ToString().Substring(0, 8)}",
                                        StartTimeMs = subClip.StartTime,
                                        EndTimeMs = subClip.EndTime,
                                        Duration = subClip.DurationDisplay,
                                        StartTimeDisplay = subClip.StartTimeDisplay,
                                        EndTimeDisplay = subClip.EndTimeDisplay,
                                        ClipColor = subClip.Color,
                                        ClipType = ClipType.SubClip,
                                        IsSelected = false,
                                        IsLoadingThumbnail = true
                                    };

                                    // Subscribe to selection changes
                                    clipInfo.PropertyChanged += ClipInfo_PropertyChanged;
                                    fileClips.Add(clipInfo);
                                }
                            }

                            // Mark the first clip of each file (whether main or sub)
                            if (fileClips.Any())
                            {
                                fileClips[0].IsFirstClipOfFile = true;
                                processedFiles[videoFile] = fileClips;
                                clips.AddRange(fileClips);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue processing other files
                            System.Diagnostics.Debug.WriteLine($"Error processing {videoFile}: {ex.Message}");
                        }
                    }
                });


                // Update UI on UI thread
                Dispatcher.Invoke(() =>
                {
                    VideoClips.Clear();
                    foreach (var clip in clips)
                    {
                        VideoClips.Add(clip);
                    }

                    StatusText = $"Loaded {clips.Count} clips from {processedFiles.Count} video files";
                    OnPropertyChanged(nameof(ClipCount));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(SelectedClipCount));
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

                var mainClipInfo = new VideoClipInfo
                {
                    VideoFilePath = videoFile,
                    VideoFileName = Path.GetFileName(videoFile),
                    SubClip = null, // Main clips don't have a SubClip object
                    ClipTitle = "Main Clip",
                    StartTimeMs = startTime,
                    EndTimeMs = endTime,
                    Duration = TimeSpan.FromMilliseconds(duration).ToString(@"mm\:ss\.fff"),
                    StartTimeDisplay = TimeSpan.FromMilliseconds(startTime).ToString(@"mm\:ss\.fff"),
                    EndTimeDisplay = TimeSpan.FromMilliseconds(endTime).ToString(@"mm\:ss\.fff"),
                    ClipColor = System.Windows.Media.Colors.DodgerBlue, // Default color for main clips
                    ClipType = ClipType.MainClip,
                    IsSelected = false,
                    IsLoadingThumbnail = true
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

                var subclipInfo = new SubclipInfo()
                {
                    VideoFile = clip.VideoFilePath,
                    StartTime = TimeSpan.FromMilliseconds(clip.StartTimeMs),
                    EndTime = TimeSpan.FromMilliseconds(clip.EndTimeMs),
                    OutputName = $"{Path.GetFileNameWithoutExtension(clip.VideoFileName)}_subclip_{startTime:mm\\-ss}_{endTime:mm\\-ss}_stabilized.mp4"
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

        public string VideoFilePath { get; set; }
        public string VideoFileName { get; set; }
        public SubClip SubClip { get; set; }
        public string ClipTitle { get; set; }
        public long StartTimeMs { get; set; }
        public long EndTimeMs { get; set; }
        public string Duration { get; set; }
        public string StartTimeDisplay { get; set; }
        public string EndTimeDisplay { get; set; }
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