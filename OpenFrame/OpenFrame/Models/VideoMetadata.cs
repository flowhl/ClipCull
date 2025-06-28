using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Models
{
    /// <summary>
    /// Model class representing video file metadata
    /// </summary>
    public class VideoMetadata : INotifyPropertyChanged
    {
        #region Private Fields
        private string _fileName;
        private long _fileSize;
        private DateTime _createdDate;
        private DateTime _modifiedDate;
        private TimeSpan _duration;
        private int _width;
        private int _height;
        private double _frameRate;
        private string _videoCodec;
        private long _videoBitrate;
        private string _audioCodec;
        private long _audioBitrate;
        private DateTime? _dateRecorded;
        private string _cameraModel;
        private bool _hasError;
        private string _errorMessage;
        #endregion

        #region Properties

        /// <summary>
        /// Name of the video file
        /// </summary>
        public string FileName
        {
            get => _fileName ?? string.Empty;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize
        {
            get => _fileSize;
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    OnPropertyChanged(nameof(FileSize));
                    OnPropertyChanged(nameof(FileSizeFormatted));
                }
            }
        }

        /// <summary>
        /// File creation date
        /// </summary>
        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                if (_createdDate != value)
                {
                    _createdDate = value;
                    OnPropertyChanged(nameof(CreatedDate));
                    OnPropertyChanged(nameof(CreatedDateFormatted));
                }
            }
        }

        /// <summary>
        /// File last modified date
        /// </summary>
        public DateTime ModifiedDate
        {
            get => _modifiedDate;
            set
            {
                if (_modifiedDate != value)
                {
                    _modifiedDate = value;
                    OnPropertyChanged(nameof(ModifiedDate));
                    OnPropertyChanged(nameof(ModifiedDateFormatted));
                }
            }
        }

        /// <summary>
        /// Video duration
        /// </summary>
        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(DurationFormatted));
                }
            }
        }

        /// <summary>
        /// Video width in pixels
        /// </summary>
        public int Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged(nameof(Width));
                    OnPropertyChanged(nameof(Resolution));
                }
            }
        }

        /// <summary>
        /// Video height in pixels
        /// </summary>
        public int Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged(nameof(Height));
                    OnPropertyChanged(nameof(Resolution));
                }
            }
        }

        /// <summary>
        /// Video frame rate
        /// </summary>
        public double FrameRate
        {
            get => _frameRate;
            set
            {
                if (Math.Abs(_frameRate - value) > 0.01)
                {
                    _frameRate = value;
                    OnPropertyChanged(nameof(FrameRate));
                    OnPropertyChanged(nameof(FrameRateFormatted));
                }
            }
        }

        /// <summary>
        /// Video codec name
        /// </summary>
        public string VideoCodec
        {
            get => _videoCodec ?? "Unknown";
            set
            {
                if (_videoCodec != value)
                {
                    _videoCodec = value;
                    OnPropertyChanged(nameof(VideoCodec));
                }
            }
        }

        /// <summary>
        /// Video bitrate in bits per second
        /// </summary>
        public long VideoBitrate
        {
            get => _videoBitrate;
            set
            {
                if (_videoBitrate != value)
                {
                    _videoBitrate = value;
                    OnPropertyChanged(nameof(VideoBitrate));
                    OnPropertyChanged(nameof(VideoBitrateFormatted));
                }
            }
        }

        /// <summary>
        /// Audio codec name
        /// </summary>
        public string AudioCodec
        {
            get => _audioCodec ?? "Unknown";
            set
            {
                if (_audioCodec != value)
                {
                    _audioCodec = value;
                    OnPropertyChanged(nameof(AudioCodec));
                }
            }
        }

        /// <summary>
        /// Audio bitrate in bits per second
        /// </summary>
        public long AudioBitrate
        {
            get => _audioBitrate;
            set
            {
                if (_audioBitrate != value)
                {
                    _audioBitrate = value;
                    OnPropertyChanged(nameof(AudioBitrate));
                    OnPropertyChanged(nameof(AudioBitrateFormatted));
                }
            }
        }

        /// <summary>
        /// Date when the video was recorded (if available in metadata)
        /// </summary>
        public DateTime? DateRecorded
        {
            get => _dateRecorded;
            set
            {
                if (_dateRecorded != value)
                {
                    _dateRecorded = value;
                    OnPropertyChanged(nameof(DateRecorded));
                    OnPropertyChanged(nameof(DateRecordedFormatted));
                    OnPropertyChanged(nameof(HasRecordingInfo));
                }
            }
        }

        /// <summary>
        /// Camera model that recorded the video (if available in metadata)
        /// </summary>
        public string CameraModel
        {
            get => _cameraModel ?? "Not available";
            set
            {
                if (_cameraModel != value)
                {
                    _cameraModel = value;
                    OnPropertyChanged(nameof(CameraModel));
                    OnPropertyChanged(nameof(HasRecordingInfo));
                }
            }
        }

        /// <summary>
        /// Indicates if there was an error loading metadata
        /// </summary>
        public bool HasError
        {
            get => _hasError;
            set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        /// <summary>
        /// Error message if loading failed
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage ?? string.Empty;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        #endregion

        #region Formatted Properties

        /// <summary>
        /// Formatted file size (e.g., "1.2 GB")
        /// </summary>
        public string FileSizeFormatted => FormatFileSize(FileSize);

        /// <summary>
        /// Formatted creation date
        /// </summary>
        public string CreatedDateFormatted => CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// Formatted modification date
        /// </summary>
        public string ModifiedDateFormatted => ModifiedDate.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// Formatted duration (e.g., "01:23:45.678")
        /// </summary>
        public string DurationFormatted => Duration.ToString(@"hh\:mm\:ss\.fff");

        /// <summary>
        /// Formatted resolution (e.g., "1920 × 1080")
        /// </summary>
        public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "Unknown";

        /// <summary>
        /// Formatted frame rate (e.g., "60.00 fps")
        /// </summary>
        public string FrameRateFormatted => FrameRate > 0 ? $"{FrameRate:F2} fps" : "Unknown";

        /// <summary>
        /// Formatted video bitrate (e.g., "25.0 Mbps")
        /// </summary>
        public string VideoBitrateFormatted => VideoBitrate > 0 ? FormatBitrate(VideoBitrate) : "Unknown";

        /// <summary>
        /// Formatted audio bitrate (e.g., "320 kbps")
        /// </summary>
        public string AudioBitrateFormatted => AudioBitrate > 0 ? FormatBitrate(AudioBitrate) : "Unknown";

        /// <summary>
        /// Formatted recording date
        /// </summary>
        public string DateRecordedFormatted => DateRecorded?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available";

        /// <summary>
        /// Indicates if recording information is available
        /// </summary>
        public bool HasRecordingInfo => DateRecorded.HasValue || (!string.IsNullOrEmpty(_cameraModel) && _cameraModel != "Not available");

        #endregion

        #region Constructor

        public VideoMetadata()
        {
            // Initialize with default values
            CreatedDate = DateTime.MinValue;
            ModifiedDate = DateTime.MinValue;
            Duration = TimeSpan.Zero;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates an error instance of VideoMetadata
        /// </summary>
        public static VideoMetadata CreateError(string errorMessage)
        {
            return new VideoMetadata
            {
                HasError = true,
                ErrorMessage = errorMessage
            };
        }

        #endregion

        #region Private Methods

        private string FormatFileSize(long bytes)
        {
            if (bytes <= 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string FormatBitrate(long bitrate)
        {
            if (bitrate <= 0) return "0 bps";

            if (bitrate >= 1_000_000)
            {
                return $"{bitrate / 1_000_000.0:F1} Mbps";
            }
            else if (bitrate >= 1_000)
            {
                return $"{bitrate / 1_000.0:F0} kbps";
            }
            else
            {
                return $"{bitrate} bps";
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
