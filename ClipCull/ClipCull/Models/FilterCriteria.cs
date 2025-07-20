using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Models
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using FFMpegCore.Builders.MetaData;
    using global::ClipCull.Controls;
    using ClipCull.Models;
    using global::ClipCull.Core;

    namespace ClipCull.Models
    {
        /// <summary>
        /// Shared filter criteria class that can be bound to by FilterControl and used by ClipBrowserControl
        /// </summary>
        public class FilterCriteria : INotifyPropertyChanged
        {
            #region Private Fields
            private string _searchText;
            private int? _minRating;
            private int? _maxRating;
            private bool? _pickStatus; // true = picked, false = rejected, null = any/none
            private ObservableCollection<Tag> _selectedTags;
            private bool _matchAllTags;
            #endregion

            #region Public Properties

            /// <summary>
            /// Search text to look for in all user metadata fields (title, description, author, location, reel, shot, camera)
            /// </summary>
            public string SearchText
            {
                get => _searchText;
                set { _searchText = value; OnPropertyChanged(); OnFilterChanged(); }
            }

            /// <summary>
            /// Minimum rating filter (1-5)
            /// </summary>
            public int? MinRating
            {
                get => _minRating;
                set { _minRating = value; OnPropertyChanged(); OnFilterChanged(); }
            }

            /// <summary>
            /// Maximum rating filter (1-5)
            /// </summary>
            public int? MaxRating
            {
                get => _maxRating;
                set { _maxRating = value; OnPropertyChanged(); OnFilterChanged(); }
            }

            /// <summary>
            /// Pick status filter: true = picked only, false = rejected only, null = all (picked, rejected, and none)
            /// </summary>
            public bool? PickStatus
            {
                get => _pickStatus;
                set { _pickStatus = value; OnPropertyChanged(); OnFilterChanged(); }
            }

            /// <summary>
            /// Tags that must be present on clips
            /// </summary>
            public ObservableCollection<Tag> SelectedTags
            {
                get => _selectedTags;
                set
                {
                    if (_selectedTags != null)
                        _selectedTags.CollectionChanged -= OnSelectedTagsChanged;

                    _selectedTags = value;

                    if (_selectedTags != null)
                        _selectedTags.CollectionChanged += OnSelectedTagsChanged;

                    OnPropertyChanged();
                    OnFilterChanged();
                }
            }

            /// <summary>
            /// If true, clips must have ALL selected tags. If false, clips must have ANY of the selected tags.
            /// </summary>
            public bool MatchAllTags
            {
                get => _matchAllTags;
                set { _matchAllTags = value; OnPropertyChanged(); OnFilterChanged(); }
            }

            #endregion

            #region Computed Properties

            /// <summary>
            /// Returns true if any filter criteria are active
            /// </summary>
            public bool IsActive =>
                !string.IsNullOrWhiteSpace(SearchText) ||
                MinRating.HasValue ||
                MaxRating.HasValue ||
                PickStatus.HasValue ||
                (SelectedTags?.Count > 0);

            #endregion
            #region Filter Matching
            /// <summary>
            /// Check if SubClip matches the search criteria
            /// </summary>
            /// <param name="subClip">The SubClip to test against</param>
            /// <returns>True if the SubClip matches search criteria</returns>
            public bool Matches(SubClip subClip)
            {
                if (subClip == null)
                    return false;

                // Only check search text for SubClips (they don't have other metadata)
                if (string.IsNullOrWhiteSpace(SearchText))
                    return false; // No search text means match

                var searchLower = SearchText.ToLowerInvariant();
                return ContainsIgnoreCase(subClip.Title, searchLower);
            }

            /// <summary>
            /// Check if UserMetadata matches all active filter criteria
            /// </summary>
            /// <param name="metadata">The metadata to test against</param>
            /// <returns>True if the metadata matches all active filter criteria</returns>
            public bool Matches(UserMetadataContent metadata)
            {
                if (metadata == null)
                    return false;

                // Search text filter - check all text fields
                if (!MatchesSearchText(metadata))
                    return false;

                // Rating filters
                if (!MatchesRatingCriteria(metadata))
                    return false;

                // Pick status filter
                if (!MatchesPickStatus(metadata))
                    return false;

                // Tags filter
                if (!MatchesTagCriteria(metadata))
                    return false;

                return true;
            }

            /// <summary>
            /// Check if metadata matches search text criteria
            /// </summary>
            private bool MatchesSearchText(UserMetadataContent metadata)
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return true;

                var searchLower = SearchText.ToLowerInvariant();

                return ContainsIgnoreCase(metadata.Title, searchLower) ||
                       ContainsIgnoreCase(metadata.Description, searchLower) ||
                       ContainsIgnoreCase(metadata.Author, searchLower) ||
                       ContainsIgnoreCase(metadata.Location, searchLower) ||
                       ContainsIgnoreCase(metadata.Reel, searchLower) ||
                       ContainsIgnoreCase(metadata.Shot, searchLower) ||
                       ContainsIgnoreCase(metadata.Camera, searchLower);
            }

            /// <summary>
            /// Check if metadata matches rating criteria
            /// </summary>
            private bool MatchesRatingCriteria(UserMetadataContent metadata)
            {
                // Check minimum rating
                if (MinRating.HasValue)
                {
                    if (!metadata.Rating.HasValue || metadata.Rating.Value < MinRating.Value)
                        return false;
                }

                // Check maximum rating
                if (MaxRating.HasValue)
                {
                    if (!metadata.Rating.HasValue || metadata.Rating.Value > MaxRating.Value)
                        return false;
                }

                return true;
            }

            /// <summary>
            /// Check if metadata matches pick status criteria
            /// </summary>
            private bool MatchesPickStatus(UserMetadataContent metadata)
            {
                if (!PickStatus.HasValue)
                    return true;

                return metadata.Pick == PickStatus.Value;
            }

            /// <summary>
            /// Check if metadata matches tag criteria
            /// </summary>
            private bool MatchesTagCriteria(UserMetadataContent metadata)
            {
                if (SelectedTags?.Count == 0)
                    return true;

                // No tags on metadata but tags are required
                if (metadata.Tags == null || metadata.Tags.Count == 0)
                    return false;

                if (MatchAllTags)
                    return MatchesAllTags(metadata);
                else
                    return MatchesAnyTag(metadata);
            }

            /// <summary>
            /// Check if metadata has ALL selected tags
            /// </summary>
            private bool MatchesAllTags(UserMetadataContent metadata)
            {
                foreach (var requiredTag in SelectedTags)
                {
                    if (!metadata.Tags.Any(t => t.Name == requiredTag.Name))
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Check if metadata has ANY of the selected tags
            /// </summary>
            private bool MatchesAnyTag(UserMetadataContent metadata)
            {
                return SelectedTags.Any(selectedTag =>
                    metadata.Tags.Any(metaTag => metaTag.Name == selectedTag.Name));
            }

            #endregion

            #region Constructor

            public FilterCriteria()
            {
                _selectedTags = new ObservableCollection<Tag>();
                _selectedTags.CollectionChanged += OnSelectedTagsChanged;
                _matchAllTags = false; // Default to "any" tag matching
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Reset all filter criteria to their default (inactive) state
            /// </summary>
            public void Reset()
            {
                SearchText = null;
                MinRating = null;
                MaxRating = null;
                PickStatus = null;
                SelectedTags?.Clear();
                MatchAllTags = SettingsHandler.Settings.FilterMustMatchAllTags;
            }

            /// <summary>
            /// Create a copy of the current filter criteria
            /// </summary>
            public FilterCriteria Clone()
            {
                var clone = new FilterCriteria
                {
                    SearchText = SearchText,
                    MinRating = MinRating,
                    MaxRating = MaxRating,
                    PickStatus = PickStatus,
                    MatchAllTags = MatchAllTags
                };

                if (SelectedTags != null)
                {
                    foreach (var tag in SelectedTags)
                    {
                        clone.SelectedTags.Add(tag);
                    }
                }

                return clone;
            }

            #endregion

            #region Events

            public event PropertyChangedEventHandler PropertyChanged;
            public event EventHandler FilterChanged;

            #endregion

            #region Private Methods

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private void OnFilterChanged()
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }

            private void OnSelectedTagsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            {
                OnFilterChanged();
            }

            private static bool ContainsIgnoreCase(string source, string searchTerm)
            {
                return !string.IsNullOrEmpty(source) && source.ToLowerInvariant().Contains(searchTerm);
            }

            #endregion
        }
    }
}
