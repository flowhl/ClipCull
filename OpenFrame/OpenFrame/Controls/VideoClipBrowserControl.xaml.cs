using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpenFrame.Core;
using OpenFrame.Models;
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

        #endregion

        #region Events

        /// <summary>
        /// Fired when clip selection changes
        /// </summary>
        public event EventHandler<ClipSelectionChangedEventArgs> ClipSelectionChanged;

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
                            if (sidecarContent?.SubClips != null && sidecarContent.SubClips.Any())
                            {
                                var fileClips = new List<VideoClipInfo>();

                                foreach (var subClip in sidecarContent.SubClips)
                                {
                                    var clipInfo = new VideoClipInfo
                                    {
                                        VideoFilePath = videoFile,
                                        VideoFileName = Path.GetFileName(videoFile),
                                        SubClip = subClip,
                                        ClipTitle = !string.IsNullOrEmpty(subClip.Title) ? subClip.Title : $"Clip {subClip.Id.ToString().Substring(0, 8)}",
                                        StartTimeMs = subClip.StartTime,
                                        EndTimeMs = subClip.EndTime,
                                        Duration = subClip.DurationDisplay,
                                        StartTimeDisplay = subClip.StartTimeDisplay,
                                        EndTimeDisplay = subClip.EndTimeDisplay,
                                        ClipColor = subClip.Color,
                                        // Don't access ColorBrush on background thread - let it be created on UI thread
                                        IsLoadingThumbnail = true
                                    };

                                    fileClips.Add(clipInfo);
                                    clips.Add(clipInfo);
                                }

                                // Mark the first clip of each file
                                if (fileClips.Any())
                                {
                                    fileClips[0].IsFirstClipOfFile = true;
                                }

                                processedFiles[videoFile] = fileClips;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue processing other files
                            System.Diagnostics.Debug.WriteLine($"Error processing {videoFile}: {ex.Message}");
                        }
                    }
                });

                // Update UI on main thread
                Dispatcher.Invoke(() =>
                {
                    VideoClips.Clear();
                    foreach (var clip in clips.OrderBy(c => c.VideoFileName).ThenBy(c => c.StartTimeMs))
                    {
                        VideoClips.Add(clip);
                    }

                    OnPropertyChanged(nameof(ClipCount));
                    StatusText = $"Loaded {ClipCount} clips from {processedFiles.Count} files";
                });

                // Load thumbnails asynchronously without blocking
                _ = Task.Run(() => LoadThumbnailsAsync(clips));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusText = $"Error: {ex.Message}");
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
}