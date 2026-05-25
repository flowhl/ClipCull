using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace ClipCull.Models
{
    public class RenderSettings : INotifyPropertyChanged
    {
        private RenderEngineType _engine = RenderEngineType.Gyroflow;
        public RenderEngineType Engine
        {
            get => _engine;
            set { _engine = value; OnPropertyChanged(); }
        }

        // Video
        private VideoCodec _videoCodec = VideoCodec.H264;
        public VideoCodec VideoCodec
        {
            get => _videoCodec;
            set { _videoCodec = value; OnPropertyChanged(); }
        }

        private QualityMode _qualityMode = QualityMode.CRF;
        public QualityMode QualityMode
        {
            get => _qualityMode;
            set { _qualityMode = value; OnPropertyChanged(); }
        }

        private int _quality = 18;
        public int Quality
        {
            get => _quality;
            set { _quality = value; OnPropertyChanged(); }
        }

        private int _bitrate;
        /// <summary>
        /// Video bitrate in kbps. 0 means not set (use CRF instead).
        /// </summary>
        public int Bitrate
        {
            get => _bitrate;
            set { _bitrate = value; OnPropertyChanged(); }
        }

        private string _preset = "medium";
        public string Preset
        {
            get => _preset;
            set { _preset = value; OnPropertyChanged(); }
        }

        private int _outputWidth;
        /// <summary>
        /// Output width in pixels. 0 means use source resolution.
        /// </summary>
        public int OutputWidth
        {
            get => _outputWidth;
            set { _outputWidth = value; OnPropertyChanged(); }
        }

        private int _outputHeight;
        /// <summary>
        /// Output height in pixels. 0 means use source resolution.
        /// </summary>
        public int OutputHeight
        {
            get => _outputHeight;
            set { _outputHeight = value; OnPropertyChanged(); }
        }

        // Audio
        private AudioCodec _audioCodec = AudioCodec.AAC;
        public AudioCodec AudioCodec
        {
            get => _audioCodec;
            set { _audioCodec = value; OnPropertyChanged(); }
        }

        private int _audioBitrate = 192;
        public int AudioBitrate
        {
            get => _audioBitrate;
            set { _audioBitrate = value; OnPropertyChanged(); }
        }

        private EqualizerSettings _equalizer = new EqualizerSettings();
        /// <summary>
        /// Audio equalizer settings. Only applied when rendering with the FFmpeg engine.
        /// </summary>
        public EqualizerSettings Equalizer
        {
            get => _equalizer;
            set { _equalizer = value ?? new EqualizerSettings(); OnPropertyChanged(); }
        }

        // Container
        private ContainerFormat _containerFormat = ContainerFormat.MP4;
        public ContainerFormat ContainerFormat
        {
            get => _containerFormat;
            set { _containerFormat = value; OnPropertyChanged(); }
        }

        // Performance
        private HardwareAcceleration _hardwareAcceleration = HardwareAcceleration.Auto;
        public HardwareAcceleration HardwareAcceleration
        {
            get => _hardwareAcceleration;
            set { _hardwareAcceleration = value; OnPropertyChanged(); }
        }

        private int _parallelRenders = 1;
        public int ParallelRenders
        {
            get => _parallelRenders;
            set { _parallelRenders = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
