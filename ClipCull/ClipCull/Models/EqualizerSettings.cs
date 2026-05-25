using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace ClipCull.Models
{
    /// <summary>
    /// 10-band ISO graphic equalizer settings. Applied during FFmpeg render only.
    /// </summary>
    public class EqualizerSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// Standard ISO center frequencies (Hz) for the 10 bands.
        /// </summary>
        public static readonly int[] BandFrequenciesHz =
            { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

        public const int BandCount = 10;
        public const double MinGainDb = -20.0;
        public const double MaxGainDb = 20.0;

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        private double _preampDb;
        /// <summary>
        /// Global preamp gain in dB applied before the band filters.
        /// </summary>
        public double PreampDb
        {
            get => _preampDb;
            set { _preampDb = value; OnPropertyChanged(); }
        }

        private double[] _bandGainsDb = new double[BandCount];
        /// <summary>
        /// Per-band gain values in dB. Index matches <see cref="BandFrequenciesHz"/>.
        /// </summary>
        public double[] BandGainsDb
        {
            get => _bandGainsDb;
            set
            {
                _bandGainsDb = (value != null && value.Length == BandCount)
                    ? value
                    : new double[BandCount];
                OnPropertyChanged();
            }
        }

        public void SetBandGain(int index, double gainDb)
        {
            if (index >= 0 && index < BandCount)
            {
                _bandGainsDb[index] = gainDb;
                OnPropertyChanged(nameof(BandGainsDb));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
