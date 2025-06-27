using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

namespace OpenFrame.Controls
{
    public partial class TimelineControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private long _duration;
        private long _currentTime;
        private bool _isDragging;
        private bool _isInitialized;
        #endregion

        #region Properties
        public long Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged(nameof(Duration));
                    UpdateTimelineDisplay();
                }
            }
        }

        public long CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_currentTime != value)
                {
                    _currentTime = value;
                    OnPropertyChanged(nameof(CurrentTime));
                    UpdatePlayheadPosition();
                    UpdateCurrentTimeDisplay();
                }
            }
        }

        public ObservableCollection<Marker> Markers { get; }
        #endregion

        #region Events
        public event EventHandler<TimelineClickedEventArgs> TimelineClicked;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Constructor
        public TimelineControl()
        {
            InitializeComponent();
            Markers = new ObservableCollection<Marker>();
            Markers.CollectionChanged += Markers_CollectionChanged;

            Loaded += TimelineControl_Loaded;
            SizeChanged += TimelineControl_SizeChanged;
        }
        #endregion

        #region Public Methods
        public void AddMarkerAtCurrentTime(string title = "")
        {
            var marker = new Marker(CurrentTime, title);
            Markers.Add(marker);
        }

        public void RemoveMarker(Marker marker)
        {
            if (marker != null)
            {
                Markers.Remove(marker);
            }
        }

        public void ClearMarkers()
        {
            Markers.Clear();
        }

        public void SeekToTime(long timeMs)
        {
            CurrentTime = Math.Max(0, Math.Min(Duration, timeMs));
            TimelineClicked?.Invoke(this, new TimelineClickedEventArgs(CurrentTime));
        }

        public void ShowMarkerEditDialog(Marker marker)
        {
            var dialog = new MarkerEditDialog(marker, CurrentTime)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                if (dialog.DeleteRequested)
                {
                    RemoveMarker(marker);
                }
                // Marker is already updated through binding
                // RefreshMarkers will be called automatically via PropertyChanged
            }
        }
        #endregion

        #region Private Methods
        private void UpdateTimelineDisplay()
        {
            if (!_isInitialized) return;

            // Update timeline track width
            TimelineTrack.Width = TimelineCanvas.ActualWidth;

            // Update duration display
            DurationDisplay.Text = FormatTime(Duration);

            // Update time labels
            UpdateTimeLabels();

            // Refresh marker positions
            RefreshMarkers();
        }

        private void UpdatePlayheadPosition()
        {
            if (!_isInitialized || Duration <= 0) return;

            var position = (double)CurrentTime / Duration * TimelineCanvas.ActualWidth;
            Canvas.SetLeft(Playhead, position - 2); // Center the playhead line
            Canvas.SetLeft(PlayheadHandle, position - 7); // Center the playhead handle

            // Debug: Make sure playhead is visible
            Playhead.Visibility = Visibility.Visible;
            PlayheadHandle.Visibility = Visibility.Visible;
        }

        private void UpdateCurrentTimeDisplay()
        {
            CurrentTimeDisplay.Text = FormatTime(CurrentTime);
        }

        private void UpdateTimeLabels()
        {
            TimeLabelsCanvas.Children.Clear();

            if (Duration <= 0 || TimelineCanvas.ActualWidth <= 0) return;

            // Calculate appropriate time intervals for labels
            var intervals = CalculateTimeIntervals();

            foreach (var interval in intervals)
            {
                if (interval > Duration) break;

                var position = (double)interval / Duration * TimelineCanvas.ActualWidth;
                var label = new TextBlock
                {
                    Text = FormatTime(interval),
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10
                };

                Canvas.SetLeft(label, position - 20); // Center the label
                Canvas.SetTop(label, 0);
                TimeLabelsCanvas.Children.Add(label);
            }
        }

        private long[] CalculateTimeIntervals()
        {
            if (Duration <= 0) return new long[0];

            var durationSeconds = Duration / 1000.0;
            var intervals = new System.Collections.Generic.List<long>();

            long intervalMs;
            if (durationSeconds <= 30)
                intervalMs = 5000; // 5 second intervals
            else if (durationSeconds <= 300)
                intervalMs = 30000; // 30 second intervals
            else if (durationSeconds <= 1800)
                intervalMs = 60000; // 1 minute intervals
            else
                intervalMs = 300000; // 5 minute intervals

            for (long time = 0; time <= Duration; time += intervalMs)
            {
                intervals.Add(time);
            }

            return intervals.ToArray();
        }

        private void RefreshMarkers()
        {
            // Remove existing marker visuals
            var markersToRemove = TimelineCanvas.Children.OfType<Polygon>().ToList();
            foreach (var marker in markersToRemove)
            {
                TimelineCanvas.Children.Remove(marker);
            }

            // Add current markers
            foreach (var marker in Markers)
            {
                AddMarkerVisual(marker);
            }
        }

        private void AddMarkerVisual(Marker marker)
        {
            if (!_isInitialized || Duration <= 0) return;

            var position = (double)marker.Timestamp / Duration * TimelineCanvas.ActualWidth;

            var markerVisual = new Polygon
            {
                Points = new PointCollection(new Point[]
                {
                    new Point(6, 0),   // Top center
                    new Point(12, 8),  // Right
                    new Point(6, 16),  // Bottom center
                    new Point(0, 8)    // Left
                }),
                Fill = new SolidColorBrush(Colors.Gold),
                Stroke = new SolidColorBrush(Color.FromRgb(204, 153, 0)),
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                Tag = marker,
                ToolTip = $"{marker.Title}\n{marker.TimeDisplay}"
            };

            markerVisual.MouseLeftButtonDown += MarkerVisual_MouseLeftButtonDown;

            Canvas.SetLeft(markerVisual, position - 6); // Center the marker
            Canvas.SetTop(markerVisual, 5); // Position above the track
            Canvas.SetZIndex(markerVisual, 2); // Set marker z-index
            TimelineCanvas.Children.Add(markerVisual);
        }

        private long CalculateTimeFromPosition(double x)
        {
            if (Duration <= 0 || TimelineCanvas.ActualWidth <= 0) return 0;

            var percentage = Math.Max(0, Math.Min(1, x / TimelineCanvas.ActualWidth));
            return (long)(percentage * Duration);
        }

        private string FormatTime(long timeMs)
        {
            var time = TimeSpan.FromMilliseconds(timeMs);
            return time.ToString(@"hh\:mm\:ss\.fff");
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Event Handlers
        private void TimelineControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitialized = true;
            UpdateTimelineDisplay();
        }

        private void TimelineControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTimelineDisplay();
        }

        private void Markers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Handle removed markers
            if (e.OldItems != null)
            {
                foreach (Marker marker in e.OldItems)
                {
                    marker.PropertyChanged -= Marker_PropertyChanged;
                }
            }

            // Handle added markers
            if (e.NewItems != null)
            {
                foreach (Marker marker in e.NewItems)
                {
                    marker.PropertyChanged += Marker_PropertyChanged;
                }
            }

            // Refresh visual markers
            RefreshMarkers();
        }

        private void Marker_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Refresh markers when any marker property changes
            RefreshMarkers();
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            TimelineCanvas.CaptureMouse();

            var position = e.GetPosition(TimelineCanvas);
            var newTime = CalculateTimeFromPosition(position.X);
            SeekToTime(newTime);
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var position = e.GetPosition(TimelineCanvas);
                var newTime = CalculateTimeFromPosition(position.X);
                SeekToTime(newTime);
            }
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            TimelineCanvas.ReleaseMouseCapture();
        }

        private void MarkerVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var markerVisual = sender as Polygon;
            var marker = markerVisual?.Tag as Marker;
            if (marker == null) return;

            if (e.ClickCount == 2) // Double click - Edit marker
            {
                ShowMarkerEditDialog(marker);
            }
            else // Single click - Seek to marker time
            {
                SeekToTime(marker.Timestamp);
            }

            e.Handled = true; // Prevent timeline click
        }
        #endregion
    }

    #region Event Args Classes
    public class TimelineClickedEventArgs : EventArgs
    {
        public long Time { get; }

        public TimelineClickedEventArgs(long time)
        {
            Time = time;
        }
    }
    #endregion
}