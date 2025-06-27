using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Models
{
    public class Marker : INotifyPropertyChanged
    {
        private long _timestamp;
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

        /// <summary>
        /// Formatted display of timestamp for UI
        /// </summary>
        public string TimeDisplay => TimeSpan.FromMilliseconds(Timestamp).ToString(@"hh\:mm\:ss\.fff");

        public Marker(long timestamp, string title = "")
        {
            _timestamp = timestamp;
            _title = title;
        }

        public Marker()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Title} ({TimeDisplay})";
        }

        public override bool Equals(object obj)
        {
            if (obj is Marker other)
            {
                return Timestamp == other.Timestamp && Title == other.Title;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Timestamp, Title);
        }
    }
}
