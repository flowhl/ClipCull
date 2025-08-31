using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
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
using System.Windows.Shapes;

namespace ClipCull.Controls
{
    /// <summary>
    /// Interaction logic for SubClipEditDialog.xaml
    /// </summary>
    public partial class SubClipEditDialog : Window
    {
        public SubClip SubClip { get; private set; }
        public bool DeleteRequested { get; private set; }
        public bool AllowDelete { get; private set; }

        public SubClipEditDialog(SubClip subClip, long currentVideoTime = 0, bool allowDelete = false)
        {
            InitializeComponent();
            SubClip = subClip;
            AllowDelete = allowDelete;

            DeleteButton.Visibility = AllowDelete ? Visibility.Visible : Visibility.Collapsed;

            // Initialize UI with subclip data
            TitleTextBox.Text = subClip.Title;
            StartTimeTextBox.Text = FormatTime(subClip.StartTime);
            EndTimeTextBox.Text = FormatTime(subClip.EndTime);
            CurrentTimeLabel.Text = $"Current position: {FormatTime(currentVideoTime)}";

            // Set color preview
            ColorPreview.Fill = new SolidColorBrush(subClip.Color);

            UpdateDurationDisplay();

            // Focus on title textbox
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAndUpdateSubClip())
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AllowDelete)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the subclip '{SubClip.Title}'?",
                "Delete SubClip",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DeleteRequested = true;
                DialogResult = true;
                Close();
            }
        }

        private void ChangeColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Simple color picker - cycle through predefined colors
            var colors = new[]
            {
                Color.FromRgb(255, 99, 132),   // Red
                Color.FromRgb(54, 162, 235),   // Blue
                Color.FromRgb(255, 205, 86),   // Yellow
                Color.FromRgb(75, 192, 192),   // Teal
                Color.FromRgb(153, 102, 255),  // Purple
                Color.FromRgb(255, 159, 64),   // Orange
                Color.FromRgb(199, 199, 199),  // Grey
                Color.FromRgb(83, 102, 255),   // Indigo
                Color.FromRgb(255, 99, 255),   // Pink
                Color.FromRgb(99, 255, 132),   // Green
            };

            var currentIndex = Array.IndexOf(colors, SubClip.Color);
            var nextIndex = (currentIndex + 1) % colors.Length;
            SubClip.Color = colors[nextIndex];
            ColorPreview.Fill = new SolidColorBrush(SubClip.Color);
        }

        private void StartTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDurationDisplay();
        }

        private void EndTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDurationDisplay();
        }

        private void UpdateDurationDisplay()
        {
            if (TryParseTime(StartTimeTextBox.Text, out long startTime) &&
                TryParseTime(EndTimeTextBox.Text, out long endTime))
            {
                var duration = endTime - startTime;
                if (duration >= 0)
                {
                    DurationLabel.Text = $"Duration: {FormatTime(duration)}";
                    DurationLabel.Foreground = new SolidColorBrush(Colors.LightGray);
                }
                else
                {
                    DurationLabel.Text = "Duration: Invalid (End time must be after start time)";
                    DurationLabel.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            else
            {
                DurationLabel.Text = "Duration: --:--:--.---";
                DurationLabel.Foreground = new SolidColorBrush(Colors.LightGray);
            }
        }

        private bool ValidateAndUpdateSubClip()
        {
            // Validate title
            var title = TitleTextBox.Text?.Trim() ?? string.Empty;

            // Validate and parse start time
            if (!TryParseTime(StartTimeTextBox.Text, out long startTime))
            {
                MessageBox.Show(
                    "Invalid start time format. Please use HH:MM:SS.mmm format (e.g., 01:23:45.678)",
                    "Invalid Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                StartTimeTextBox.Focus();
                StartTimeTextBox.SelectAll();
                return false;
            }

            // Validate and parse end time
            if (!TryParseTime(EndTimeTextBox.Text, out long endTime))
            {
                MessageBox.Show(
                    "Invalid end time format. Please use HH:MM:SS.mmm format (e.g., 01:23:45.678)",
                    "Invalid Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                EndTimeTextBox.Focus();
                EndTimeTextBox.SelectAll();
                return false;
            }

            // Validate times are not negative
            if (startTime < 0 || endTime < 0)
            {
                MessageBox.Show(
                    "Timestamps cannot be negative.",
                    "Invalid Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Validate end time is after start time
            if (endTime <= startTime)
            {
                MessageBox.Show(
                    "End time must be after start time.",
                    "Invalid Time Range",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                EndTimeTextBox.Focus();
                EndTimeTextBox.SelectAll();
                return false;
            }

            // Update subclip
            SubClip.Title = title;
            SubClip.StartTime = startTime;
            SubClip.EndTime = endTime;

            return true;
        }

        private bool TryParseTime(string timeString, out long milliseconds)
        {
            milliseconds = 0;

            if (string.IsNullOrWhiteSpace(timeString))
                return false;

            try
            {
                // Try to parse as TimeSpan format (HH:MM:SS.mmm)
                if (TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out TimeSpan timeSpan))
                {
                    milliseconds = (long)timeSpan.TotalMilliseconds;
                    return true;
                }

                // Try alternative formats
                if (TimeSpan.TryParseExact(timeString, @"mm\:ss\.fff", CultureInfo.InvariantCulture, out timeSpan))
                {
                    milliseconds = (long)timeSpan.TotalMilliseconds;
                    return true;
                }

                if (TimeSpan.TryParseExact(timeString, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out timeSpan))
                {
                    milliseconds = (long)timeSpan.TotalMilliseconds;
                    return true;
                }

                if (TimeSpan.TryParseExact(timeString, @"mm\:ss", CultureInfo.InvariantCulture, out timeSpan))
                {
                    milliseconds = (long)timeSpan.TotalMilliseconds;
                    return true;
                }

                // Try parsing as seconds
                if (double.TryParse(timeString, out double seconds))
                {
                    milliseconds = (long)(seconds * 1000);
                    return true;
                }
            }
            catch
            {
                // Parsing failed
            }

            return false;
        }

        private string FormatTime(long timeMs)
        {
            var time = TimeSpan.FromMilliseconds(timeMs);
            return time.ToString(@"hh\:mm\:ss\.fff");
        }
    }
}
