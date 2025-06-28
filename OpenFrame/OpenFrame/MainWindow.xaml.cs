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
using AvalonDock.Layout.Serialization;
using System.ComponentModel;

namespace OpenFrame;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private WindowSettings _windowSettings;
    private DispatcherTimer _layoutSaveTimer;
    private bool _isLayoutLoaded = false;


    public MainWindow()
    {
        Logger.Init();
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

        // Load settings
        _windowSettings = LayoutManager.LoadSettings();

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

        // Load custom layout after everything is initialized
        this.Loaded += MainWindow_Loaded;
        this.Closing += MainWindow_Closing;
        // Track window changes
        this.LocationChanged += (s, e) => UpdateWindowSettings();
        this.SizeChanged += (s, e) => UpdateWindowSettings();
        this.StateChanged += (s, e) => UpdateWindowSettings();

        // Track layout changes with a timer to avoid excessive saves
        InitializeLayoutSaveTimer();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Only restore custom layout if user has made changes before
        if (_windowSettings.HasCustomLayout && !string.IsNullOrEmpty(_windowSettings.DockLayoutXml))
        {
            RestoreCustomLayout();
        }

        // Now start monitoring for layout changes
        DockManager.LayoutUpdated += DockManager_LayoutUpdated;
        _isLayoutLoaded = true;

        VideoMetadataViewer.DataContext = VideoPreview;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {

    }

    private void InitializeLayoutSaveTimer()
    {
        _layoutSaveTimer = new DispatcherTimer();
        _layoutSaveTimer.Interval = TimeSpan.FromMilliseconds(1000); // Save 1 second after last change
        _layoutSaveTimer.Tick += (s, e) =>
        {
            _layoutSaveTimer.Stop();
            SaveDockLayout();
        };
    }

    private void OpenVideoButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select Video File",
            Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v|" +
                    "MP4 Files|*.mp4|" +
                    "MOV Files|*.mov|" +
                    "All Files|*.*",
            FilterIndex = 1,
            RestoreDirectory = true
        };

        if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string selectedFile = openFileDialog.FileName;

            // Validate file exists and is accessible
            if (!File.Exists(selectedFile))
            {
                UpdateStatus("Error: Selected file does not exist.", true);
                return;
            }

            LoadVideoFile(selectedFile);
        }
    }

    #region File Loading

    public void LoadVideoFile(string filePath)
    {
        try
        {
            CurrentFileLabel.Text = $"Loading: {Path.GetFileName(filePath)}";
            UpdateStatus("Loading video...", false);

            ApplySidecarContent(GetSidecarContent(filePath));

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
        ApplySidecarContent(GetSidecarContent(VideoPreview.CurrentVideoPath));
    }

    private void SaveSidecarButton_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPreview == null || string.IsNullOrEmpty(VideoPreview.CurrentVideoPath))
        {
            UpdateStatus("No video loaded to save sidecar content.", true);
            return;
        }
        SaveSidecarContent(VideoPreview?.CurrentVideoPath);
    }

    public SidecarContent GetSidecarContent(string videoFile)
    {
        string sidecarPath = Path.ChangeExtension(videoFile, ".xml");
        if (!File.Exists(sidecarPath))
        {
            UpdateStatus($"No sidecar file found for {Path.GetFileName(videoFile)}", false);
            return new SidecarContent();
        }

        var sidecarContent = Globals.DeserializeFromFile<SidecarContent>(sidecarPath);
        if (sidecarContent != null)
            return sidecarContent;

        UpdateStatus($"Failed to load sidecar content from {Path.GetFileName(sidecarPath)}", true);
        throw new InvalidDataException("Invalid sidecar content format.");
    }

    public void SaveSidecarContent(string videoFile)
    {
        string sidecarPath = Path.ChangeExtension(videoFile, ".xml");
        var currentSidecar = GetCurrentStateAsSidecar();
        if (currentSidecar == null)
        {
            UpdateStatus("No sidecar content to save.", true);
            Logger.LogInfo("No sidecar content to save.");
            return;
        }
        try
        {
            Globals.SerializeToFile(currentSidecar, sidecarPath);
            UpdateStatus($"Sidecar content saved to {Path.GetFileName(sidecarPath)}", false);
            Logger.LogInfo($"Sidecar content saved to {sidecarPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save sidecar content", ex);
        }
    }

    public SidecarContent GetCurrentStateAsSidecar()
    {
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

        UpdateStatus("Sidecar content applied successfully.", false);
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

    #region Window Position Persistence

    private void RestoreWindowPosition()
    {
        try
        {
            this.Left = _windowSettings.Left;
            this.Top = _windowSettings.Top;
            this.Width = _windowSettings.Width;
            this.Height = _windowSettings.Height;
            this.WindowState = _windowSettings.WindowState;

            EnsureWindowIsVisible();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to restore window position", ex);
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void EnsureWindowIsVisible()
    {
        var windowRect = new System.Drawing.Rectangle(
            (int)this.Left, (int)this.Top,
            (int)this.Width, (int)this.Height);

        bool isVisible = false;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(windowRect))
            {
                isVisible = true;
                break;
            }
        }

        if (!isVisible)
        {
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void UpdateWindowSettings()
    {
        if (this.WindowState == WindowState.Normal)
        {
            _windowSettings.Left = this.Left;
            _windowSettings.Top = this.Top;
            _windowSettings.Width = this.ActualWidth;
            _windowSettings.Height = this.ActualHeight;
        }
        _windowSettings.WindowState = this.WindowState;
    }

    #endregion

    #region Dock Layout Persistence

    private void RestoreCustomLayout()
    {
        try
        {
            var layoutSerializer = new XmlLayoutSerializer(DockManager);
            layoutSerializer.LayoutSerializationCallback += LayoutSerializer_LayoutSerializationCallback;

            using (var stringReader = new StringReader(_windowSettings.DockLayoutXml))
            {
                layoutSerializer.Deserialize(stringReader);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to restore custom layout, using default", ex);
            // Don't reset flag here - let user keep their preference
        }
    }

    private void LayoutSerializer_LayoutSerializationCallback(object sender, LayoutSerializationCallbackEventArgs e)
    {
        // Match ContentId from XAML to actual controls
        switch (e.Model.ContentId)
        {
            case "videoPreview":
                e.Content = FindName("VideoPreview");
                break;
            case "timeline":
                e.Content = FindName("TimelineControl");
                break;
            case "properties":
                e.Content = FindPropertiesPanel();
                break;
            case "markers":
                e.Content = FindMarkersPanel();
                break;
            default:
                e.Cancel = true; // Don't restore unknown panels
                break;
        }
    }

    private object FindPropertiesPanel()
    {
        // Find the properties panel content from your XAML
        // This should match whatever you have inside the Properties LayoutAnchorable
        // You might need to adjust this based on your actual XAML structure
        return FindName("PropertiesContent") ?? CreatePropertiesContent();
    }

    private object FindMarkersPanel()
    {
        // Find the markers panel content from your XAML
        return FindName("MarkersContent") ?? CreateMarkersContent();
    }

    private UIElement CreatePropertiesContent()
    {
        // Fallback: recreate properties content if not found
        var stackPanel = new System.Windows.Controls.StackPanel();
        // Add your properties controls here
        return stackPanel;
    }

    private UIElement CreateMarkersContent()
    {
        // Fallback: recreate markers content if not found
        var stackPanel = new System.Windows.Controls.StackPanel();
        // Add your markers controls here
        return stackPanel;
    }

    private void DockManager_LayoutUpdated(object sender, EventArgs e)
    {
        if (!_isLayoutLoaded) return; // Don't save during initial load

        // Restart the timer - only save after user stops moving things
        _layoutSaveTimer.Stop();
        _layoutSaveTimer.Start();
    }

    private void SaveDockLayout()
    {
        try
        {
            var layoutSerializer = new XmlLayoutSerializer(DockManager);
            using (var stringWriter = new StringWriter())
            {
                layoutSerializer.Serialize(stringWriter);
                _windowSettings.DockLayoutXml = stringWriter.ToString();
                _windowSettings.HasCustomLayout = true; // Mark that user has customized
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save dock layout", ex);
        }
    }

    #endregion

    private void AddMarkerButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void ResetLayoutButton_Click(object sender, RoutedEventArgs e)
    {

    }
}