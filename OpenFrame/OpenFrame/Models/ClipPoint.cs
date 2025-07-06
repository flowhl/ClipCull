using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Models
{
    public enum ClipPointType
    {
        InPoint,
        OutPoint
    }

    public class ClipPoint : INotifyPropertyChanged
    {
        private long _timestamp;
        private ClipPointType _type;
        private string _title;

        public long Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged(nameof(Timestamp));
                    OnPropertyChanged(nameof(TimeDisplay));
                }
            }
        }

        public ClipPointType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(DisplayName));
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
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string TimeDisplay => TimeSpan.FromMilliseconds(Timestamp).ToString(@"hh\:mm\:ss\.fff");

        public string DisplayName => string.IsNullOrEmpty(Title) ?
            (Type == ClipPointType.InPoint ? "In Point" : "Out Point") :
            Title;

        public ClipPoint(long timestamp, ClipPointType type, string title = "")
        {
            _timestamp = timestamp;
            _type = type;
            _title = title;
        }

        public ClipPoint()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{DisplayName} ({TimeDisplay})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is ClipPoint other)
            {
                return Timestamp == other.Timestamp &&
                       Type == other.Type &&
                       string.Equals(Title, other.Title, StringComparison.OrdinalIgnoreCase);
            }
            return base.Equals(obj);
        }
    }
}
