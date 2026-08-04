using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ClipCull.Models.Import
{
    /// <summary>
    /// One media file discovered on the source, shown as a row in the import grid.
    /// Filesystem fields are filled immediately by the scanner; duration/resolution/thumbnail
    /// are filled lazily in the background; conflict fields are filled once a target is chosen.
    /// </summary>
    public class ImportFileItem : INotifyPropertyChanged
    {
        // ----- Filesystem info (filled immediately) -----
        public string FullPath { get; set; }
        public string FileName { get; set; }

        /// <summary>Directory of this file relative to the source root (for display). Empty for root files.</summary>
        public string RelativeSubPath { get; set; }

        public long SizeBytes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public ImportMediaKind MediaKind { get; set; }

        public string SizeDisplay => FormatSize(SizeBytes);
        public string CreatedDisplay => CreatedDate.ToString("yyyy-MM-dd HH:mm");
        public string ModifiedDisplay => ModifiedDate.ToString("yyyy-MM-dd HH:mm");

        /// <summary>Returns the timestamp used for date-based selection/structuring.</summary>
        public DateTime GetDate(ImportDateBasis basis) => basis == ImportDateBasis.Created ? CreatedDate : ModifiedDate;

        // ----- Selection -----
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        // ----- Lazy probe results -----
        private string _thumbnailPath;
        public string ThumbnailPath
        {
            get => _thumbnailPath;
            set { if (_thumbnailPath != value) { _thumbnailPath = value; OnPropertyChanged(); } }
        }

        private bool _isProbing;
        public bool IsProbing
        {
            get => _isProbing;
            set { if (_isProbing != value) { _isProbing = value; OnPropertyChanged(); } }
        }

        private long? _durationMs;
        public long? DurationMs
        {
            get => _durationMs;
            set { if (_durationMs != value) { _durationMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationDisplay)); } }
        }

        public string DurationDisplay => _durationMs.HasValue
            ? TimeSpan.FromMilliseconds(_durationMs.Value).ToString(@"hh\:mm\:ss")
            : string.Empty;

        private string _resolution;
        public string Resolution
        {
            get => _resolution;
            set { if (_resolution != value) { _resolution = value; OnPropertyChanged(); } }
        }

        // ----- Conflict info (filled once target chosen) -----
        private ConflictStatus _conflictStatus = ConflictStatus.None;
        public ConflictStatus ConflictStatus
        {
            get => _conflictStatus;
            set
            {
                if (_conflictStatus != value)
                {
                    _conflictStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasConflict));
                    OnPropertyChanged(nameof(ConflictSummary));
                }
            }
        }

        private ConflictResolution _conflictResolutionChoice = ConflictResolution.Import;
        public ConflictResolution ConflictResolutionChoice
        {
            get => _conflictResolutionChoice;
            set { if (_conflictResolutionChoice != value) { _conflictResolutionChoice = value; OnPropertyChanged(); } }
        }

        /// <summary>Path of the existing file that triggered the conflict (for the tooltip).</summary>
        public string ConflictExistingPath { get; set; }

        private List<ConflictResolution> _availableResolutions = new() { ConflictResolution.Import };
        public List<ConflictResolution> AvailableResolutions
        {
            get => _availableResolutions;
            set { _availableResolutions = value; OnPropertyChanged(); }
        }

        public bool HasConflict => _conflictStatus != ConflictStatus.None;

        public string ConflictSummary => _conflictStatus switch
        {
            ConflictStatus.DestinationSameContent => "Already imported",
            ConflictStatus.DestinationNameClash => "Name clash",
            ConflictStatus.ExistsElsewhere => "Exists elsewhere",
            _ => string.Empty
        };

        /// <summary>Computed destination full path (set by the organizer before import).</summary>
        public string DestinationPath { get; set; }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.#} {units[unit]}";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
