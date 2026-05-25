using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ClipCull.Models
{
    /// <summary>
    /// Information about a video clip
    /// </summary>
    public class VideoClipInfo : INotifyPropertyChanged
    {
        private string _thumbnailPath;
        private bool _isLoadingThumbnail;
        private bool _isSelected;
        private SolidColorBrush _clipColorBrush;
        private int _subClipCount;

        public UserMetadataContent UserMetadata { get; set; }

        public string VideoFilePath { get; set; }
        public string VideoFileName { get; set; }
        public SubClip SubClip { get; set; }
        public string ClipTitle { get; set; }
        public long StartTimeMs { get; set; }
        public long EndTimeMs { get; set; }
        public long DurationMs
        {
            get
            {
                return EndTimeMs - StartTimeMs;
            }
        }
        public string DurationString
        {
            get
            {
                return TimeSpan.FromMilliseconds(EndTimeMs - StartTimeMs).ToString(@"mm\:ss\.fff");
            }
        }
        public string StartTimeDisplay
        {
            get
            {
                return TimeSpan.FromMilliseconds(StartTimeMs).ToString(@"mm\:ss\.fff");
            }
        }
        public string EndTimeDisplay
        {
            get
            {
                return TimeSpan.FromMilliseconds(EndTimeMs).ToString(@"mm\:ss\.fff");
            }
        }
        public Color ClipColor { get; set; }
        public bool IsFirstClipOfFile { get; set; }

        /// <summary>
        /// Returns the rating used for display in the clips view: subclip's own rating for subclips,
        /// otherwise the main clip's UserMetadata.Rating.
        /// </summary>
        public int? EffectiveRating => IsSubClip ? SubClip?.Rating : UserMetadata?.Rating;

        /// <summary>
        /// Raise a change notification for <see cref="EffectiveRating"/> after the underlying source has been updated.
        /// </summary>
        public void NotifyEffectiveRatingChanged()
        {
            OnPropertyChanged(nameof(EffectiveRating));
        }

        /// <summary>
        /// Type of clip (Main or Sub)
        /// </summary>
        public ClipType ClipType { get; set; }

        /// <summary>
        /// Whether this is a main clip (convenience property)
        /// </summary>
        public bool IsMainClip => ClipType == ClipType.MainClip;

        /// <summary>
        /// Whether this is a sub clip (convenience property)
        /// </summary>
        public bool IsSubClip => ClipType == ClipType.SubClip;

        /// <summary>
        /// Whether this clip is selected via checkbox
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush ClipColorBrush
        {
            get
            {
                if (_clipColorBrush == null)
                {
                    _clipColorBrush = new SolidColorBrush(ClipColor);
                    _clipColorBrush.Freeze(); // Make it thread-safe
                }
                return _clipColorBrush;
            }
        }

        public string ThumbnailPath
        {
            get => _thumbnailPath;
            set
            {
                if (_thumbnailPath != value)
                {
                    _thumbnailPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoadingThumbnail
        {
            get => _isLoadingThumbnail;
            set
            {
                if (_isLoadingThumbnail != value)
                {
                    _isLoadingThumbnail = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SubClipCount
        {
            get => _subClipCount;
            set
            {
                if (_subClipCount != value)
                {
                    _subClipCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SubClipCountDisplay));
                }
            }
        }

        public string SubClipCountDisplay => SubClipCount > 0 ? $"({SubClipCount})" : "";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
