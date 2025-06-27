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

namespace OpenFrame;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        Logger.Init();
        InitializeComponent();
        InitializeEventHandlers();
    }

    private void InitializeEventHandlers()
    {
        // Subscribe to VideoPreviewControl events
        VideoPreview.VideoLoaded += VideoPreview_VideoLoaded;
        VideoPreview.VideoLoadFailed += VideoPreview_VideoLoadFailed;
        VideoPreview.PlaybackStateChanged += VideoPreview_PlaybackStateChanged;
    }

    private void OpenVideoButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Title = "Select Video File",
            Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v|" +
                    "MP4 Files|*.mp4|" +
                    "MOV Files|*.mov|" +
                    "All Files|*.*",
            FilterIndex = 1,
            RestoreDirectory = true
        };

        if (openFileDialog.ShowDialog() == true)
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
}