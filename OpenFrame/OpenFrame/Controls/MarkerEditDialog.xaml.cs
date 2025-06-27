using OpenFrame.Models;
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

namespace OpenFrame.Controls
{
    /// <summary>
    /// Interaction logic for MarkerEditDialog.xaml
    /// </summary>
    public partial class MarkerEditDialog : Window
    {
        public Marker Marker { get; private set; }
        public bool DeleteRequested { get; private set; }

        public MarkerEditDialog(Marker marker, long currentVideoTime = 0)
        {
            InitializeComponent();
            Marker = marker;

            // Initialize UI with marker data
            TitleTextBox.Text = marker.Title;
            TimestampTextBox.Text = FormatTime(marker.Timestamp);
            CurrentTimeLabel.Text = $"Current position: {FormatTime(currentVideoTime)}";

            // Focus on title textbox
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAndUpdateMarker())
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
            var result = MessageBox.Show(
                $"Are you sure you want to delete the marker '{Marker.Title}'?",
                "Delete Marker",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DeleteRequested = true;
                DialogResult = true;
                Close();
            }
        }

        private bool ValidateAndUpdateMarker()
        {
            // Validate title
            var title = TitleTextBox.Text?.Trim() ?? string.Empty;

            // Validate and parse timestamp
            if (!TryParseTime(TimestampTextBox.Text, out long timestamp))
            {
                MessageBox.Show(
                    "Invalid time format. Please use HH:MM:SS.mmm format (e.g., 01:23:45.678)",
                    "Invalid Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TimestampTextBox.Focus();
                TimestampTextBox.SelectAll();
                return false;
            }

            // Validate timestamp is not negative
            if (timestamp < 0)
            {
                MessageBox.Show(
                    "Timestamp cannot be negative.",
                    "Invalid Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TimestampTextBox.Focus();
                TimestampTextBox.SelectAll();
                return false;
            }

            // Update marker
            Marker.Title = title;
            Marker.Timestamp = timestamp;

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