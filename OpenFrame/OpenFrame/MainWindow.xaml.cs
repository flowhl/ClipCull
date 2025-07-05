using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;
using OpenFrame.Controls;
using OpenFrame.Core;
using OpenFrame.Models;
using System.Windows.Threading;
using System.ComponentModel;
using OpenFrame.Core.Update;
using OpenFrame.Extensions;
using MessageBox = System.Windows.MessageBox;
using System.Collections.ObjectModel;
using FFMpegCore.Enums;

namespace OpenFrame;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public ICommand LoadLayoutCommand { get; private set; }
    public ICommand SaveLayoutCommand { get; private set; }

    public MainWindow()
    {
        Logger.Init();
        SettingsHandler.Initialize();
        Directory.CreateDirectory(Globals.ExternalPath);
        try
        {
#if DEBUG
#else
                AppUpdates.AppUpdateManager.CheckForUpdates();
#endif
        }
        catch (Exception ex)
        {
            Logger.LogError("Error checking for updates: " + ex.Message, ex);
        }

        //Call before InitializeComponent
        LoadLayoutCommand = LayoutManager.CreateLoadLayoutCommand();
        SaveLayoutCommand = LayoutManager.CreateSaveLayoutCommand();

        InitializeComponent();
        InitializeEventHandlers();
    }

    private void InitializeEventHandlers()
    {
        // Subscribe to VideoPreviewControl events
        VideoPreview.VideoLoaded += VideoPreview_VideoLoaded;
        VideoPreview.VideoLoadFailed += VideoPreview_VideoLoadFailed;
        VideoPreview.PlaybackStateChanged += VideoPreview_PlaybackStateChanged;

        FolderTree.FolderSelected += FolderTree_FolderSelected;
        FolderTree.FileSelected += FolderTree_FileSelected;

        VideoClipBrowser.ClipSelectionChanged += VideoClipBrowser_ClipSelectionChanged;
        clipPreview.VideoLoaded += ClipPreview_VideoLoaded;

        this.Loaded += MainWindow_Loaded;
        this.Closing += MainWindow_Closing;
    }

    private void ClipPreview_VideoLoaded(object? sender, VideoLoadedEventArgs e)
    {
        var selectedClip = VideoClipBrowser.SelectedClip;
        var seekTo = TimeSpan.FromMilliseconds(selectedClip.StartTimeMs);
        clipPreview.SeekTo(seekTo);
        clipPreview.timelineControl.SubClips.Clear();
        clipPreview.timelineControl.InPoint = null;
        clipPreview.timelineControl.OutPoint = null;
        if (selectedClip.SubClip != null)
        {
            clipPreview.timelineControl.SubClips.Add(selectedClip.SubClip);
        }
        else
        {
            clipPreview.timelineControl.InPoint = new ClipPoint(selectedClip.StartTimeMs, ClipPointType.InPoint);
            clipPreview.timelineControl.OutPoint = new ClipPoint(selectedClip.EndTimeMs, ClipPointType.OutPoint);
        }
    }

    private void VideoClipBrowser_ClipSelectionChanged(object? sender, ClipSelectionChangedEventArgs e)
    {
        clipPreview.LoadVideo(e.SelectedFile);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LayoutManager.InitializeLayoutManagement(this);

        VideoMetadataViewer.DataContext = VideoPreview;
        UserMetadataViewer.DataContext = VideoPreview;
        CheckForFfmpeg();
    }

    private async Task CheckForFfmpeg()
    {
        var updateService = new FFmpegUpdateService();
        try
        {
            await updateService.EnsureFFmpegAsync(Globals.ExternalPath);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("FFmpeg update failed " + ex.GetFullDetails());
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Force save the layout before the window actually closes
        if (SaveLayoutCommand?.CanExecute(null) == true)
        {
            var parameter = new OpenFrame.Behaviors.DockLayoutParameter
            {
                DockingManager = EditingDockManager,
                ManagerName = "EditingDockManager" // or whatever name you're using
            };

            // Manually serialize and save
            try
            {
                using (var stringWriter = new StringWriter())
                {
                    var xmlLayout = new AvalonDock.Layout.Serialization.XmlLayoutSerializer(EditingDockManager);
                    xmlLayout.Serialize(stringWriter);
                    parameter.LayoutXml = stringWriter.ToString();
                }

                SaveLayoutCommand.Execute(parameter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Manual save failed: {ex.Message}");
            }
        }
    }

    private void OpenVideoButton_Click(object sender, RoutedEventArgs e)
    {
        // Check for unsaved changes before opening a new video
        if (!AllowNavigation())
            return;

        string title = "Select Video File";
        string filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v|" +
                "MP4 Files|*.mp4|" +
                "MOV Files|*.mov|" +
                "All Files|*.*";
        string selectedFile = DialogHelper.ChooseFile(title, filter);

        if (selectedFile.IsNullOrEmpty())
            return;

        // Validate file exists and is accessible
        if (!File.Exists(selectedFile))
        {
            UpdateStatus("Error: Selected file does not exist.", true);
            return;
        }

        LoadVideoFile(selectedFile);
    }

    #region File Loading

    public void LoadVideoFile(string filePath)
    {
        // Check for unsaved changes before opening a new video
        if (!AllowNavigation())
            return;

        try
        {
            CurrentFileLabel.Text = $"Loading: {Path.GetFileName(filePath)}";
            UpdateStatus("Loading video...", false);

            ApplySidecarContent(SidecarService.GetSidecarContent(filePath));

            VideoPreview.LoadVideo(filePath);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading video: {ex.Message}", true);
            CurrentFileLabel.Text = "No video loaded";
        }
    }


    private void ReloadSidecarButton_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPreview == null || string.IsNullOrEmpty(VideoPreview.CurrentVideoPath))
        {
            UpdateStatus("No video loaded to reload sidecar content.", true);
            return;
        }
        ApplySidecarContent(SidecarService.GetSidecarContent(VideoPreview.CurrentVideoPath));
    }

    private void SaveSidecarButton_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPreview == null || string.IsNullOrEmpty(VideoPreview.CurrentVideoPath))
        {
            UpdateStatus("No video loaded to save sidecar content.", true);
            return;
        }
        SidecarService.SaveSidecarContent(GetCurrentStateAsSidecar(), VideoPreview?.CurrentVideoPath);
    }

    public SidecarContent GetCurrentStateAsSidecar()
    {
        //Return null if no video is loaded
        if (VideoPreview.CurrentVideoPath == null)
            return null;

        var sidecarContent = new SidecarContent();
        if (VideoPreview == null || VideoPreview.timelineControl == null)
        {
            UpdateStatus("Video preview or timeline control is not initialized.", true);
            return null;
        }
        sidecarContent.SubClips = VideoPreview.timelineControl.SubClips.ToList();
        sidecarContent.Markers = VideoPreview.timelineControl.Markers.ToList();
        sidecarContent.InPoint = VideoPreview.timelineControl.InPoint;
        sidecarContent.OutPoint = VideoPreview.timelineControl.OutPoint;
        sidecarContent.UserMetadata = VideoPreview.UserMetadata;

        return sidecarContent;
    }

    public void ApplySidecarContent(SidecarContent sidecarContent)
    {
        if (sidecarContent == null)
        {
            UpdateStatus("No sidecar content to apply.", true);
            return;
        }
        // Clear existing markers and subclips
        VideoPreview.timelineControl.ClearMarkers();
        VideoPreview.timelineControl.ClearSubClips();
        // Apply markers
        if (sidecarContent.Markers == null || sidecarContent.Markers.Count == 0)
        {
            UpdateStatus("No markers found in sidecar content.", false);
        }
        else
        {
            UpdateStatus($"Applying {sidecarContent.Markers.Count} markers from sidecar content.", false);
            foreach (var marker in sidecarContent.Markers)
            {
                VideoPreview.timelineControl.Markers.Add(marker);
            }
        }

        // Apply subclips
        if (sidecarContent.SubClips == null || sidecarContent.SubClips.Count == 0)
        {
            UpdateStatus("No subclips found in sidecar content.", false);
        }
        else
        {
            UpdateStatus($"Applying {sidecarContent.SubClips.Count} subclips from sidecar content.", false);
            foreach (var subClip in sidecarContent.SubClips)
            {
                VideoPreview.timelineControl.SubClips.Add(subClip);
            }
        }

        VideoPreview.timelineControl.InPoint = sidecarContent.InPoint;
        VideoPreview.timelineControl.OutPoint = sidecarContent.OutPoint;
        VideoPreview.UserMetadata = sidecarContent.UserMetadata;

        UpdateStatus("Sidecar content applied successfully.", false);
    }

    /// <summary>
    /// Checks if the current state of the video preview allows navigation to a new video.
    /// </summary>
    /// <returns></returns>
    public bool AllowNavigation()
    {
        SidecarContent currentState = GetCurrentStateAsSidecar();
        SidecarContent savedState = SidecarService.GetSidecarContent(VideoPreview.CurrentVideoPath);
        if (currentState != null && !SidecarService.Equals(currentState, savedState))
        {
            var result = MessageBox.Show("You have unsaved changes. Do you want to save them before loading a new video?",
                                         "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                SaveSidecarButton_Click(null, null);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return false;
            }
        }
        return true;
    }

    #endregion

    private void VideoPreview_VideoLoaded(object sender, Controls.VideoLoadedEventArgs e)
    {
        // Update UI when video loads successfully
        CurrentFileLabel.Text = $"Loaded: {Path.GetFileName(e.VideoPath)}";
        UpdateStatus($"Video loaded successfully - Duration: {e.Duration}", false);
    }

    private void VideoPreview_VideoLoadFailed(object sender, Controls.VideoLoadFailedEventArgs e)
    {
        // Handle video load failure
        CurrentFileLabel.Text = "Failed to load video";
        UpdateStatus($"Failed to load video: {e.ErrorMessage}", true);
    }

    private void VideoPreview_PlaybackStateChanged(object sender, Controls.PlaybackStateChangedEventArgs e)
    {
        // Optional: Update status based on playback state
        string stateText = e.IsPlaying ? "Playing" : "Paused";
        // UpdateStatus($"Video {stateText}", false);
    }

    private void UpdateStatus(string message, bool isError = false)
    {
        StatusLabel.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        StatusLabel.Foreground = isError ?
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightCoral) :
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up video resources
        VideoPreview?.Dispose();
        base.OnClosed(e);
    }

    #region Filetreeview
    private void FolderTree_FolderSelected(object sender, Controls.FolderSelectedEventArgs e)
    {
        // When user selects a folder, you could:
        // 1. Update status
        UpdateStatus($"Selected folder: {e.FolderPath}");

        // 2. Auto-load first video file in folder (optional)
        // LoadFirstVideoFromFolder(e.FolderPath);
    }

    private void FolderTree_FileSelected(object sender, Controls.FileSelectedEventArgs e)
    {
        // When user selects a video file, load it
        if (IsVideoFile(e.FilePath))
        {
            LoadVideoFile(e.FilePath);
        }
    }

    private bool IsVideoFile(string filePath)
    {
        var videoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };
        return videoExtensions.Contains(Path.GetExtension(filePath).ToLower());
    }
    #endregion
    private void ResetLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutManager.ResetLayout("EditingDockManager");
        MessageBox.Show("Layout has been reset. Please restart the application to see the changes.",
                       "Layout Reset", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenClipFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string folder = DialogHelper.ChooseFolder("Select Clip Folder");

        if (folder.IsNullOrEmpty())
            return;

        if (!Directory.Exists(folder))
            return;

        VideoClipBrowser.FolderPath = folder;
    }
}