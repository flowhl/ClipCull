using LibVLCSharp.Shared;
using MaterialDesignThemes.Wpf;
using ClipCull.Core;
using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ClipCull.Controls
{
    public partial class VideoPreviewControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private Media _media;
        private DispatcherTimer _positionTimer;
        private bool _isDisposed = false;
        private int _rotation = 0;
        #endregion

        #region Properties
        public MediaPlayer MediaPlayer
        {
            get => _mediaPlayer;
            private set
            {
                _mediaPlayer = value;
                OnPropertyChanged(nameof(MediaPlayer));
            }
        }

        private string _currentVideoPath;
        public string CurrentVideoPath
        {
            get => _currentVideoPath;
            private set
            {
                _currentVideoPath = value;
                OnPropertyChanged(nameof(CurrentVideoPath));
            }
        }

        private VideoMetadata _currentVideoMetadata;
        public VideoMetadata CurrentVideoMetadata
        {
            get => _currentVideoMetadata;
            set
            {
                _currentVideoMetadata = value;
                OnPropertyChanged(nameof(CurrentVideoMetadata));
            }
        }

        public int Rotation
        {
            get => _rotation;
            set
            {
                if (_rotation != value)
                {
                    _rotation = value;
                    OnPropertyChanged(nameof(Rotation));
                    DisposeVLC();
                    InitializeVLC(_rotation);
                    LoadVideo(CurrentVideoPath); // Reload video with new rotation
                    UserMetadata.Rotation = _rotation;
                }
            }
        }

        private bool _readonly;
        public bool Readonly
        {
            get
            {
                return _readonly;
            }
            set
            {
                _readonly = value;
                if (timelineControl != null)
                    timelineControl.Readonly = value;
                OnPropertyChanged(nameof(Readonly));
            }
        }

        private UserMetadataContent _userMetadata;
        public UserMetadataContent UserMetadata
        {
            get => _userMetadata;
            set
            {
                _userMetadata = value;
                OnPropertyChanged(nameof(UserMetadata));
            }
        }

        #endregion

        #region Events
        public event EventHandler<VideoLoadedEventArgs> VideoLoaded;
        public event EventHandler<VideoLoadFailedEventArgs> VideoLoadFailed;
        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Constructor
        public VideoPreviewControl()
        {
            InitializeComponent();
            NoVideoOverlay.Visibility = Visibility.Visible;
            Panel.SetZIndex(NoVideoOverlay, 1000);
            DataContext = this;
            InitializeVLC(Rotation);
            InitializeTimer();
            InitializeTimelineEvents();
        }
        #endregion

        #region Initialization
        private void InitializeVLC(int rotationAngle = 0)
        {
            string[] args = null;
            if (rotationAngle != 0)
            {
                //args = new string[] { };
                args = new string[] { "--video-filter=transform", "--transform-type=" + rotationAngle };
            }
            try
            {
                LibVLCSharp.Shared.Core.Initialize();
                if (args != null)
                {
                    _libVLC = new LibVLC(args);
                }
                else
                {
                    _libVLC = new LibVLC();
                }
                MediaPlayer = new MediaPlayer(_libVLC);

                // Subscribe to MediaPlayer events
                MediaPlayer.EndReached += MediaPlayer_EndReached;
                MediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
                MediaPlayer.Playing += MediaPlayer_Playing;
                MediaPlayer.Paused += MediaPlayer_Paused;
                MediaPlayer.Stopped += MediaPlayer_Stopped;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize VLC: {ex.Message}", "VLC Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            BorderVideoArea.SizeChanged += BorderVideoArea_SizeChanged;
        }

        private void DisposeVLC()
        {
            // Unsubscribe from MediaPlayer events
            MediaPlayer.EndReached -= MediaPlayer_EndReached;
            MediaPlayer.EncounteredError -= MediaPlayer_EncounteredError;
            MediaPlayer.Playing -= MediaPlayer_Playing;
            MediaPlayer.Paused -= MediaPlayer_Paused;
            MediaPlayer.Stopped -= MediaPlayer_Stopped;

            MediaPlayer.Stop();
            MediaPlayer.Dispose();
            MediaPlayer = null;
            _libVLC.Dispose();
            _libVLC = null;
            GC.Collect();
        }

        private void BorderVideoArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //refresh as somehow something is white
            //BorderVideoArea.InvalidateVisual();
        }

        private void InitializeTimer()
        {
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _positionTimer.Tick += PositionTimer_Tick;
        }

        private void InitializeTimelineEvents()
        {
            timelineControl.TimelineClicked += TimelineControl_TimelineClicked;

            HotkeyController.OnNext += HotkeyController_OnNext;
            HotkeyController.OnPrevious += HotkeyController_OnPrevious;
            HotkeyController.OnNextSmall += HotkeyController_OnNextSmall;
            HotkeyController.OnPreviousSmall += HotkeyController_OnPreviousSmall;
            HotkeyController.OnTogglePlay += HotkeyController_OnTogglePlay;
        }

        #endregion

        #region Public Methods
        public void LoadVideo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                VideoLoadFailed?.Invoke(this, new VideoLoadFailedEventArgs("File does not exist", filePath));
                return;
            }

            try
            {
                // Clean up previous media
                StopVideo();
                _media?.Dispose();

                //Read metadata
                try
                {
                    CurrentVideoMetadata = VideoMetadataReader.ReadMetadata(filePath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to read metadata for {filePath}: {ex.Message}");
                    CurrentVideoMetadata = null;
                }

                // Create new media
                _media = new Media(_libVLC, filePath);
                MediaPlayer.Media = _media;

                // Parse media to get duration info
                Task.Run(async () =>
                {
                    await _media.Parse(MediaParseOptions.ParseNetwork);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        NoVideoOverlay.Visibility = Visibility.Collapsed;
                        VideoView.Visibility = Visibility.Visible;
                        CurrentVideoPath = filePath;
                        UpdateMediaInfo();
                        EnableControls(true);

                        // Start playing and immediately pause to get first frame
                        var prevVolume = MediaPlayer.Volume;
                        MediaPlayer.Volume = 0; // Mute to avoid sound during first frame
                        MediaPlayer.Play();
                        Task.Delay(100).Wait();
                        MediaPlayer.Pause();
                        MediaPlayer.Position = 0;
                        MediaPlayer.Volume = prevVolume; // Restore volume

                        VideoLoaded?.Invoke(this, new VideoLoadedEventArgs(filePath, _media.Duration));
                        _positionTimer.Start();
                    });
                });
            }
            catch (Exception ex)
            {
                VideoLoadFailed?.Invoke(this, new VideoLoadFailedEventArgs(ex.Message, filePath));
            }
        }

        public void Play()
        {
            if (MediaPlayer?.Media != null)
            {
                MediaPlayer.Play();
            }
        }

        public void Pause()
        {
            if (MediaPlayer?.IsPlaying == true)
            {
                MediaPlayer.Pause();
            }
        }

        public void Stop()
        {
            StopVideo();
        }

        public void SeekTo(TimeSpan time)
        {
            if (MediaPlayer?.Media != null && MediaPlayer.Length > 0)
            {
                float position = (float)(time.TotalMilliseconds / MediaPlayer.Length);
                MediaPlayer.Position = Math.Max(0, Math.Min(1, position));
            }
        }

        public void SeekToTime(long timeMs)
        {
            if (MediaPlayer?.Media != null && MediaPlayer.Length > 0)
            {
                MediaPlayer.Time = Math.Max(0, Math.Min(MediaPlayer.Length, timeMs));
            }
        }
        #endregion

        #region Private Methods
        private void StopVideo()
        {
            _positionTimer.Stop();
            MediaPlayer?.Stop();
            timelineControl.CurrentTime = 0;
        }

        private void UpdateMediaInfo()
        {
            if (_media?.Duration > 0)
            {
                timelineControl.Duration = _media.Duration;
            }
        }

        private void EnableControls(bool enabled)
        {
            PlayPauseButton.IsEnabled = enabled;
            StopButton.IsEnabled = enabled;
            FrameBackwardButton.IsEnabled = enabled;
            FrameForwardButton.IsEnabled = enabled;
            Skip10BackwardButton.IsEnabled = enabled;
            Skip10ForwardButton.IsEnabled = enabled;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Event Handlers - VLC MediaPlayer
        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PlayPauseIcon.Kind = PackIconKind.Pause;
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(true));
            });
        }

        private void MediaPlayer_Paused(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PlayPauseIcon.Kind = PackIconKind.Play;
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(false));
            });
        }

        private void MediaPlayer_Stopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PlayPauseIcon.Kind = PackIconKind.Play;
                timelineControl.CurrentTime = 0;
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(false));
            });
        }

        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PlayPauseIcon.Kind = PackIconKind.Play;
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(false));
            });
        }

        private void MediaPlayer_EncounteredError(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                VideoLoadFailed?.Invoke(this, new VideoLoadFailedEventArgs("Playback error occurred", CurrentVideoPath));
            });
        }
        #endregion

        #region Event Handlers - UI Controls
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer?.Media == null) return;

            if (MediaPlayer.IsPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer?.Media != null)
            {
                MediaPlayer.Stop();
                MediaPlayer.Position = 0;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Volume = (int)e.NewValue;
                VolumeLabel.Text = $"{(int)e.NewValue}%";
            }
        }

        private void FrameBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer?.Media != null)
            {
                Logger.LogWarning("Frame stepping is not implemented in VLC. This button will not work as expected.");
                //MediaPlayer.PreviousFrame();
            }
        }

        private void FrameForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer?.Media != null)
            {
                MediaPlayer.NextFrame();
            }
        }

        private void Skip10BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer?.Media != null && MediaPlayer.Length > 0)
            {
                var currentTime = MediaPlayer.Time;
                var newTime = Math.Max(0, currentTime - 10000); // 10 seconds = 10000ms
                MediaPlayer.Time = newTime;
            }
        }

        private void Skip10ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer?.Media != null && MediaPlayer.Length > 0)
            {
                var currentTime = MediaPlayer.Time;
                var newTime = Math.Min(MediaPlayer.Length, currentTime + 10000); // 10 seconds = 10000ms
                MediaPlayer.Time = newTime;
            }
        }

        private void TimelineControl_TimelineClicked(object sender, TimelineClickedEventArgs e)
        {
            SeekToTime(e.Time);
        }

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (MediaPlayer?.Media != null && MediaPlayer.Length > 0)
            {
                timelineControl.CurrentTime = MediaPlayer.Time;
            }
        }

        private void HotkeyController_OnTogglePlay()
        {
            if (!IsVisible) return;
            PlayPauseButton_Click(null, null);
        }

        private void HotkeyController_OnPrevious()
        {
            if (!IsVisible) return;
            Skip10BackwardButton_Click(null, null);
        }

        private void HotkeyController_OnNext()
        {
            if (!IsVisible) return;
            Skip10ForwardButton_Click(null, null);
        }

        private void HotkeyController_OnPreviousSmall()
        {
            if (!IsVisible) return;
            FrameBackwardButton_Click(null, null);
        }

        private void HotkeyController_OnNextSmall()
        {
            if (!IsVisible) return;
            FrameForwardButton_Click(null, null);
        }

        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_isDisposed) return;

            BorderVideoArea.SizeChanged -= BorderVideoArea_SizeChanged;

            _positionTimer?.Stop();
            _positionTimer = null;

            _media?.Dispose();
            MediaPlayer?.Dispose();
            _libVLC?.Dispose();

            _isDisposed = true;
        }
        #endregion
    }

    #region Event Args Classes
    public class VideoLoadedEventArgs : EventArgs
    {
        public string VideoPath { get; }
        public long Duration { get; }

        public VideoLoadedEventArgs(string videoPath, long duration)
        {
            VideoPath = videoPath;
            Duration = duration;
        }
    }

    public class VideoLoadFailedEventArgs : EventArgs
    {
        public string ErrorMessage { get; }
        public string VideoPath { get; }

        public VideoLoadFailedEventArgs(string errorMessage, string videoPath)
        {
            ErrorMessage = errorMessage;
            VideoPath = videoPath;
        }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; }

        public PlaybackStateChangedEventArgs(bool isPlaying)
        {
            IsPlaying = isPlaying;
        }
    }
    #endregion
}