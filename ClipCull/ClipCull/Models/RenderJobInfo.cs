using System;
using System.ComponentModel;

namespace ClipCull.Models
{
    public class RenderJobInfo : INotifyPropertyChanged
    {
        private string _videoFile;
        public string VideoFile
        {
            get => _videoFile;
            set
            {
                _videoFile = value;
                OnPropertyChanged(nameof(VideoFile));
            }
        }

        private TimeSpan _startTime;
        public TimeSpan StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                OnPropertyChanged(nameof(StartTime));
                OnPropertyChanged(nameof(DurationString));
            }
        }

        private TimeSpan _endTime;
        public TimeSpan EndTime
        {
            get => _endTime;
            set
            {
                _endTime = value;
                OnPropertyChanged(nameof(EndTime));
                OnPropertyChanged(nameof(DurationString));
            }
        }

        private string _outputName;
        public string OutputName
        {
            get => _outputName;
            set
            {
                _outputName = value;
                OnPropertyChanged(nameof(OutputName));
            }
        }

        private bool _rendering;
        public bool Rendering
        {
            get => _rendering;
            set
            {
                _rendering = value;
                OnPropertyChanged(nameof(Rendering));
            }
        }

        private bool _rendered;
        public bool Rendered
        {
            get => _rendered;
            set
            {
                _rendered = value;
                OnPropertyChanged(nameof(Rendered));
            }
        }

        private string _status;
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string DurationString
        {
            get
            {
                return "Duration: " +
                    (EndTime - StartTime).TotalSeconds.ToString("0.00") + " seconds";
            }
        }

        public int Rotation { get; set; }

        /// <summary>
        /// Per-clip equalizer settings (loaded from the clip's sidecar).
        /// Only honored by render engines that support audio filtering (FFmpeg).
        /// </summary>
        public EqualizerSettings Equalizer { get; set; }

        // New properties
        private RenderProgress _progress;
        public RenderProgress Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private double _progressPercentage;
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                OnPropertyChanged(nameof(HasError));
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
