using ClipCull.Core;
using ClipCull.Models;
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

namespace ClipCull.Controls
{
    public partial class TimelineControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private long _duration;
        private long _currentTime;
        private bool _isDragging;
        private bool _isInitialized;
        private ClipPoint _inPoint;
        private ClipPoint _outPoint;
        private SubClip _currentSubClip;
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

                    // Force refresh if we have a current subclip (for live preview)
                    if (_currentSubClip != null)
                    {
                        RefreshSubClips();
                    }
                }
            }
        }

        public ClipPoint InPoint
        {
            get => _inPoint;
            set
            {
                if (_inPoint != value)
                {
                    if (_inPoint != null)
                        _inPoint.PropertyChanged -= ClipPoint_PropertyChanged;

                    _inPoint = value;

                    if (_inPoint != null)
                        _inPoint.PropertyChanged += ClipPoint_PropertyChanged;

                    OnPropertyChanged(nameof(InPoint));
                    UpdateTimelineDisplay();
                }
            }
        }

        public ClipPoint OutPoint
        {
            get => _outPoint;
            set
            {
                if (_outPoint != value)
                {
                    if (_outPoint != null)
                        _outPoint.PropertyChanged -= ClipPoint_PropertyChanged;

                    _outPoint = value;

                    if (_outPoint != null)
                        _outPoint.PropertyChanged += ClipPoint_PropertyChanged;

                    OnPropertyChanged(nameof(OutPoint));
                    UpdateTimelineDisplay();
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
                OnPropertyChanged(nameof(Readonly));
            }
        }

        public ObservableCollection<Marker> Markers { get; }
        public ObservableCollection<SubClip> SubClips { get; }
        #endregion

        #region Events
        public event EventHandler<TimelineClickedEventArgs> TimelineClicked;
        public event EventHandler<ClipPointEventArgs> InPointSet;
        public event EventHandler<ClipPointEventArgs> OutPointSet;
        public event EventHandler<SubClipEventArgs> SubClipCreated;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Constructor
        public TimelineControl()
        {
            InitializeComponent();
            OnPropertyChanged(nameof(Readonly));
            Markers = new ObservableCollection<Marker>();
            SubClips = new ObservableCollection<SubClip>();

            Markers.CollectionChanged += Markers_CollectionChanged;
            SubClips.CollectionChanged += SubClips_CollectionChanged;

            Loaded += TimelineControl_Loaded;
            SizeChanged += TimelineControl_SizeChanged;

            HotkeyController.OnSetInPoint += HotkeyController_OnSetInPoint;
            HotkeyController.OnSetOutPoint += HotkeyController_OnSetOutPoint;
            HotkeyController.OnMarker += HotkeyController_OnMarker;
            HotkeyController.OnSubclipStart += HotkeyController_OnSubclipStart;
            HotkeyController.OnSubclipEnd += HotkeyController_OnSubclipEnd;

            Focusable = true;
        }
        #endregion

        #region Public Methods - Existing
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
            if (Readonly)
                return;

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
            }
        }
        #endregion

        #region Public Methods - New I/O and SubClip Features
        public void SetInPoint()
        {
            InPoint = new ClipPoint(CurrentTime, ClipPointType.InPoint);
            InPointSet?.Invoke(this, new ClipPointEventArgs(InPoint));
        }

        public void SetOutPoint()
        {
            OutPoint = new ClipPoint(CurrentTime, ClipPointType.OutPoint);
            OutPointSet?.Invoke(this, new ClipPointEventArgs(OutPoint));
        }

        public void ClearInPoint()
        {
            InPoint = null;
        }

        public void ClearOutPoint()
        {
            OutPoint = null;
        }

        public void ClearInOutPoints()
        {
            InPoint = null;
            OutPoint = null;
        }

        public void StartSubClip()
        {
            if (_currentSubClip != null)
            {
                // Finish current subclip first
                FinishSubClip();
            }

            _currentSubClip = new SubClip(CurrentTime, CurrentTime, $"SubClip {SubClips.Count + 1}");
            ForceRefreshAll(); // Immediate visual update
        }

        public void FinishSubClip()
        {
            if (_currentSubClip != null)
            {
                _currentSubClip.EndTime = CurrentTime;

                if (_currentSubClip.IsValid)
                {
                    SubClips.Add(_currentSubClip);
                    SubClipCreated?.Invoke(this, new SubClipEventArgs(_currentSubClip));
                }

                _currentSubClip = null;
                ForceRefreshAll(); // Immediate visual update
            }
        }

        public void RemoveSubClip(SubClip subClip)
        {
            if (subClip != null)
            {
                SubClips.Remove(subClip);
            }
        }

        public void ClearSubClips()
        {
            SubClips.Clear();
            _currentSubClip = null;
            ForceRefreshAll(); // Ensure immediate visual update
        }

        public void ShowClipPointEditDialog(ClipPoint clipPoint)
        {
            if (Readonly)
                return;

            var dialog = new ClipPointEditDialog(clipPoint, CurrentTime)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                if (dialog.DeleteRequested)
                {
                    if (clipPoint == InPoint)
                        ClearInPoint();
                    else if (clipPoint == OutPoint)
                        ClearOutPoint();
                }
            }
        }

        public void ShowSubClipEditDialog(SubClip subClip)
        {
            if (Readonly)
                return;
            var dialog = new SubClipEditDialog(subClip, CurrentTime)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                if (dialog.DeleteRequested)
                {
                    RemoveSubClip(subClip);
                }
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

            // Force complete refresh of all visual elements
            ForceRefreshAll();
        }

        private void UpdatePlayheadPosition()
        {
            if (!_isInitialized || Duration <= 0) return;

            var position = (double)CurrentTime / Duration * TimelineCanvas.ActualWidth;
            Canvas.SetLeft(Playhead, position - 2);
            Canvas.SetLeft(PlayheadHandle, position - 7);

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

                Canvas.SetLeft(label, position - 20);
                Canvas.SetTop(label, 0);
                TimeLabelsCanvas.Children.Add(label);
            }
        }

        private long[] CalculateTimeIntervals()
        {
            if (Duration <= 0) return new long[0];

            var durationSeconds = Duration / 1000.0;
            var intervals = new List<long>();

            long intervalMs;
            if (durationSeconds <= 30)
                intervalMs = 5000;
            else if (durationSeconds <= 300)
                intervalMs = 30000;
            else if (durationSeconds <= 1800)
                intervalMs = 60000;
            else
                intervalMs = 300000;

            for (long time = 0; time <= Duration; time += intervalMs)
            {
                intervals.Add(time);
            }

            return intervals.ToArray();
        }

        private void RefreshMarkers()
        {
            // Remove existing marker visuals
            var markersToRemove = TimelineCanvas.Children.OfType<Polygon>()
                .Where(p => p.Tag is Marker).ToList();
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

        private void RefreshClipPoints()
        {
            // Remove existing clip point visuals and I/O range
            var clipPointsToRemove = TimelineCanvas.Children.OfType<Rectangle>()
                .Where(r => r.Tag is ClipPoint || r.Tag?.ToString() == "InOutRange").ToList();
            foreach (var clipPoint in clipPointsToRemove)
            {
                TimelineCanvas.Children.Remove(clipPoint);
            }

            // Add current clip points
            if (InPoint != null)
                AddClipPointVisual(InPoint);
            if (OutPoint != null)
                AddClipPointVisual(OutPoint);
        }

        private void RefreshInOutRange()
        {
            // Remove existing I/O range background
            var rangeToRemove = TimelineCanvas.Children.OfType<Rectangle>()
                .Where(r => r.Tag?.ToString() == "InOutRange").ToList();
            foreach (var range in rangeToRemove)
            {
                TimelineCanvas.Children.Remove(range);
            }

            // Add I/O range background if both points exist
            if (InPoint != null && OutPoint != null && Duration > 0)
            {
                var startTime = Math.Min(InPoint.Timestamp, OutPoint.Timestamp);
                var endTime = Math.Max(InPoint.Timestamp, OutPoint.Timestamp);

                var startPosition = (double)startTime / Duration * TimelineCanvas.ActualWidth;
                var endPosition = (double)endTime / Duration * TimelineCanvas.ActualWidth;
                var width = Math.Max(2, endPosition - startPosition);

                var ioRangeVisual = new Rectangle
                {
                    Width = width,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromArgb(64, 0, 122, 204)), // Semi-transparent #FF007ACC
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                    StrokeThickness = 1,
                    Tag = "InOutRange",
                    ToolTip = $"I/O Range\nDuration: {TimeSpan.FromMilliseconds(endTime - startTime):hh\\:mm\\:ss\\.fff}"
                };

                Canvas.SetLeft(ioRangeVisual, startPosition);
                Canvas.SetTop(ioRangeVisual, 11);
                Canvas.SetZIndex(ioRangeVisual, 1); // Behind timeline track
                TimelineCanvas.Children.Add(ioRangeVisual);
            }
        }

        private void RefreshSubClips()
        {
            // Remove ALL existing subclip visuals (including current one)
            var subClipsToRemove = TimelineCanvas.Children.OfType<Rectangle>()
                .Where(r => r.Tag is SubClip || r.Tag?.ToString() == "CurrentSubClip").ToList();
            foreach (var subClip in subClipsToRemove)
            {
                TimelineCanvas.Children.Remove(subClip);
            }

            // Add current subclips
            foreach (var subClip in SubClips)
            {
                AddSubClipVisual(subClip);
            }

            // Add current subclip being created
            if (_currentSubClip != null)
            {
                AddCurrentSubClipVisual();
            }
        }

        private void ForceRefreshAll()
        {
            if (!_isInitialized) return;

            // Force complete refresh of all visual elements
            RefreshMarkers();
            RefreshClipPoints();
            RefreshSubClips();
            UpdatePlayheadPosition();
        }

        private void AddMarkerVisual(Marker marker)
        {
            if (!_isInitialized || Duration <= 0) return;

            var position = (double)marker.Timestamp / Duration * TimelineCanvas.ActualWidth;

            var markerVisual = new Polygon
            {
                Points = new PointCollection(new Point[]
                {
                    new Point(6, 0),
                    new Point(12, 8),
                    new Point(6, 16),
                    new Point(0, 8)
                }),
                Fill = new SolidColorBrush(Colors.Gold),
                Stroke = new SolidColorBrush(Color.FromRgb(204, 153, 0)),
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                Tag = marker,
                ToolTip = $"{marker.Title}\n{marker.TimeDisplay}"
            };

            markerVisual.MouseLeftButtonDown += MarkerVisual_MouseLeftButtonDown;

            Canvas.SetLeft(markerVisual, position - 6);
            Canvas.SetTop(markerVisual, 5);
            Canvas.SetZIndex(markerVisual, 4);
            TimelineCanvas.Children.Add(markerVisual);
        }

        private void AddClipPointVisual(ClipPoint clipPoint)
        {
            if (!_isInitialized || Duration <= 0) return;

            var position = (double)clipPoint.Timestamp / Duration * TimelineCanvas.ActualWidth;

            var clipPointVisual = new Rectangle
            {
                Width = 3,
                Height = 26,
                Fill = new SolidColorBrush(Color.FromRgb(0, 122, 204)), // #FF007ACC for both I/O points
                Cursor = Cursors.Hand,
                Tag = clipPoint,
                ToolTip = $"{clipPoint.DisplayName}\n{clipPoint.TimeDisplay}"
            };

            clipPointVisual.MouseLeftButtonDown += ClipPointVisual_MouseLeftButtonDown;

            Canvas.SetLeft(clipPointVisual, position - 1.5);
            Canvas.SetTop(clipPointVisual, 3);
            Canvas.SetZIndex(clipPointVisual, 3);
            TimelineCanvas.Children.Add(clipPointVisual);

            // Add I/O range background if both points exist
            RefreshInOutRange();
        }

        private void AddSubClipVisual(SubClip subClip)
        {
            if (!_isInitialized || Duration <= 0) return;

            var startPosition = (double)subClip.StartTime / Duration * TimelineCanvas.ActualWidth;
            var endPosition = (double)subClip.EndTime / Duration * TimelineCanvas.ActualWidth;
            var width = Math.Max(2, endPosition - startPosition);

            var subClipVisual = new Rectangle
            {
                Width = width,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromArgb(128, subClip.Color.R, subClip.Color.G, subClip.Color.B)),
                Stroke = subClip.ColorBrush,
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                Tag = subClip,
                ToolTip = $"{subClip.Title}\n{subClip.StartTimeDisplay} - {subClip.EndTimeDisplay}\nDuration: {subClip.DurationDisplay}"
            };

            subClipVisual.MouseLeftButtonDown += SubClipVisual_MouseLeftButtonDown;

            Canvas.SetLeft(subClipVisual, startPosition);
            Canvas.SetTop(subClipVisual, 11);
            Canvas.SetZIndex(subClipVisual, 2);
            TimelineCanvas.Children.Add(subClipVisual);
        }

        private void AddCurrentSubClipVisual()
        {
            if (!_isInitialized || Duration <= 0 || _currentSubClip == null) return;

            var startPosition = (double)_currentSubClip.StartTime / Duration * TimelineCanvas.ActualWidth;
            var currentPosition = (double)CurrentTime / Duration * TimelineCanvas.ActualWidth;
            var width = Math.Max(2, currentPosition - startPosition);

            var subClipVisual = new Rectangle
            {
                Width = width,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromArgb(100, _currentSubClip.Color.R, _currentSubClip.Color.G, _currentSubClip.Color.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, _currentSubClip.Color.R, _currentSubClip.Color.G, _currentSubClip.Color.B)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Tag = "CurrentSubClip",
                ToolTip = "Current SubClip (in progress)"
            };

            Canvas.SetLeft(subClipVisual, startPosition);
            Canvas.SetTop(subClipVisual, 11);
            Canvas.SetZIndex(subClipVisual, 2);
            TimelineCanvas.Children.Add(subClipVisual);
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
            if (e.OldItems != null)
            {
                foreach (Marker marker in e.OldItems)
                {
                    marker.PropertyChanged -= Marker_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (Marker marker in e.NewItems)
                {
                    marker.PropertyChanged += Marker_PropertyChanged;
                }
            }

            RefreshMarkers();
        }

        private void SubClips_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SubClip subClip in e.OldItems)
                {
                    subClip.PropertyChanged -= SubClip_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (SubClip subClip in e.NewItems)
                {
                    subClip.PropertyChanged += SubClip_PropertyChanged;
                }
            }

            // Force immediate refresh when collection changes
            ForceRefreshAll();
        }

        private void Marker_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshMarkers();
        }

        private void ClipPoint_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshClipPoints();
        }

        private void SubClip_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshSubClips();
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            TimelineCanvas.CaptureMouse();
            Focus(); // Enable keyboard shortcuts

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

            if (e.ClickCount == 2)
            {
                ShowMarkerEditDialog(marker);
            }
            else
            {
                SeekToTime(marker.Timestamp);
            }

            e.Handled = true;
        }

        private void ClipPointVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clipPointVisual = sender as Rectangle;
            var clipPoint = clipPointVisual?.Tag as ClipPoint;
            if (clipPoint == null) return;

            if (e.ClickCount == 2)
            {
                ShowClipPointEditDialog(clipPoint);
            }
            else
            {
                SeekToTime(clipPoint.Timestamp);
            }

            e.Handled = true;
        }

        private void SubClipVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var subClipVisual = sender as Rectangle;
            var subClip = subClipVisual?.Tag as SubClip;
            if (subClip == null) return;

            if (e.ClickCount == 2)
            {
                ShowSubClipEditDialog(subClip);
            }
            else
            {
                // Seek to start of subclip
                SeekToTime(subClip.StartTime);
            }

            e.Handled = true;
        }

        #region Button Click Handlers
        private void SetInPointButton_Click(object sender, RoutedEventArgs e)
        {
            SetInPoint();
            UpdateStatusDisplay("In Point set at " + CurrentTimeDisplay.Text);
        }

        private void SetOutPointButton_Click(object sender, RoutedEventArgs e)
        {
            SetOutPoint();
            UpdateStatusDisplay("Out Point set at " + CurrentTimeDisplay.Text);
        }

        private void ClearInOutButton_Click(object sender, RoutedEventArgs e)
        {
            ClearInOutPoints();
            UpdateStatusDisplay("In/Out Points cleared");
        }

        private void StartSubClipButton_Click(object sender, RoutedEventArgs e)
        {
            StartSubClip();
            UpdateStatusDisplay("SubClip started at " + CurrentTimeDisplay.Text);
        }

        private void FinishSubClipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSubClip != null)
            {
                FinishSubClip();
                UpdateStatusDisplay("SubClip finished at " + CurrentTimeDisplay.Text);
            }
            else
            {
                UpdateStatusDisplay("No SubClip in progress");
            }
        }

        private void AddMarkerButton_Click(object sender, RoutedEventArgs e)
        {
            AddMarkerAtCurrentTime();
            UpdateStatusDisplay("Marker added at " + CurrentTimeDisplay.Text);
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all markers, clip points, and subclips?",
                "Clear All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearMarkers();
                ClearInOutPoints();
                ClearSubClips();
                UpdateStatusDisplay("All markers and clips cleared");
            }
        }

        private void UpdateStatusDisplay(string message)
        {
            if (StatusDisplay != null)
            {
                StatusDisplay.Text = message;

                // Clear status after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += (s, e) =>
                {
                    StatusDisplay.Text = "";
                    timer.Stop();
                };
                timer.Start();
            }
        }
        #endregion

        #region Hotkeys
        private void HotkeyController_OnSubclipEnd()
        {
            if (!IsVisible) return;
            FinishSubClip();
        }

        private void HotkeyController_OnSubclipStart()
        {
            if (!IsVisible) return;
            StartSubClip();
        }

        private void HotkeyController_OnMarker()
        {
            if (!IsVisible) return;
            AddMarkerAtCurrentTime();
        }

        private void HotkeyController_OnSetOutPoint()
        {
            if (!IsVisible) return;
            SetOutPoint();
        }

        private void HotkeyController_OnSetInPoint()
        {
            if (!IsVisible) return;
            SetInPoint();
        }
        #endregion
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

    public class ClipPointEventArgs : EventArgs
    {
        public ClipPoint ClipPoint { get; }

        public ClipPointEventArgs(ClipPoint clipPoint)
        {
            ClipPoint = clipPoint;
        }
    }

    public class SubClipEventArgs : EventArgs
    {
        public SubClip SubClip { get; }

        public SubClipEventArgs(SubClip subClip)
        {
            SubClip = subClip;
        }
    }
    #endregion
}