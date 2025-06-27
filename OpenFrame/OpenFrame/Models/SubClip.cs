using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace OpenFrame.Models
{
    public class SubClip : INotifyPropertyChanged
    {
        private static readonly Random _random = new Random();
        private long _startTime;
        private long _endTime;
        private string _title;
        private Color _color;
        private Guid _id;

        public Guid Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public long StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                    OnPropertyChanged(nameof(StartTimeDisplay));
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(DurationDisplay));
                }
            }
        }

        public long EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged(nameof(EndTime));
                    OnPropertyChanged(nameof(EndTimeDisplay));
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(DurationDisplay));
                }
            }
        }

        public string Title
        {
            get => _title ?? string.Empty;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged(nameof(Color));
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        public SolidColorBrush ColorBrush => new SolidColorBrush(Color);

        public long Duration => EndTime - StartTime;

        public string StartTimeDisplay => TimeSpan.FromMilliseconds(StartTime).ToString(@"hh\:mm\:ss\.fff");
        public string EndTimeDisplay => TimeSpan.FromMilliseconds(EndTime).ToString(@"hh\:mm\:ss\.fff");
        public string DurationDisplay => TimeSpan.FromMilliseconds(Duration).ToString(@"hh\:mm\:ss\.fff");

        public bool IsValid => EndTime > StartTime;

        public SubClip(long startTime, long endTime, string title = "")
        {
            _id = Guid.NewGuid();
            _startTime = startTime;
            _endTime = endTime;
            _title = title;
            _color = GenerateRandomColor();
        }

        public SubClip(Guid id, long startTime, long endTime, string title, Color color)
        {
            _id = id;
            _startTime = startTime;
            _endTime = endTime;
            _title = title;
            _color = color;
        }

        public SubClip()
        {
        }

        private static Color GenerateRandomColor()
        {
            // Generate vibrant, saturated colors
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

            return colors[_random.Next(colors.Length)];
        }

        public bool ContainsTime(long timestamp)
        {
            return timestamp >= StartTime && timestamp <= EndTime;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Title} ({StartTimeDisplay} - {EndTimeDisplay})";
        }

        public override bool Equals(object obj)
        {
            if (obj is SubClip other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
