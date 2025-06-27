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

            try
            {
                // Update UI immediately
                CurrentFileLabel.Text = $"Loading: {Path.GetFileName(selectedFile)}";
                UpdateStatus("Loading video...", false);

                // Load video in preview control
                VideoPreview.LoadVideo(selectedFile);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading video: {ex.Message}", true);
                CurrentFileLabel.Text = "No video loaded";
            }
        }
    }

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