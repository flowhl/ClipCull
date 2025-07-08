using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenFrame.Core;
using OpenFrame.Models;
using OpenFrame.Models.OpenFrame.Models;

namespace OpenFrame.Controls
{
    /// <summary>
    /// Filter control for filtering video clips based on user metadata criteria
    /// </summary>
    public partial class ClipFilterControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private FilterCriteria _filterCriteria;
        private bool _isUpdating = false;
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler FilterChanged;
        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty FilterCriteriaProperty =
            DependencyProperty.Register(nameof(FilterCriteria), typeof(FilterCriteria), typeof(ClipFilterControl),
                new PropertyMetadata(null, OnFilterCriteriaChanged));

        public FilterCriteria FilterCriteria
        {
            get => (FilterCriteria)GetValue(FilterCriteriaProperty);
            set => SetValue(FilterCriteriaProperty, value);
        }

        private static void OnFilterCriteriaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClipFilterControl control)
            {
                control.OnFilterCriteriaChanged((FilterCriteria)e.OldValue, (FilterCriteria)e.NewValue);
            }
        }

        #endregion

        #region Constructor

        public ClipFilterControl()
        {
            InitializeComponent();
            DataContext = this;

            // Set up TaggingControl with available tags
            var availableTags = new ObservableCollection<Tag>();
            SettingsHandler.Settings.Tags.ForEach(tag => availableTags.Add(tag));
            TagFilterControl.AvailableTags = availableTags;

            this.Loaded += ClipFilterControl_Loaded;
        }

        private void ClipFilterControl_Loaded(object sender, RoutedEventArgs e)
        {
            _filterCriteria.SelectedTags.CollectionChanged += SelectedTags_CollectionChanged;
            UpdateUI();
        }

        private void SelectedTags_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                UpdateUI();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Private Methods

        private void OnFilterCriteriaChanged(FilterCriteria oldValue, FilterCriteria newValue)
        {
            // Unsubscribe from old criteria
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= FilterCriteria_PropertyChanged;
                try
                {
                    oldValue.SelectedTags.CollectionChanged -= SelectedTags_CollectionChanged;
                }
                catch { }
            }

            _filterCriteria = newValue;

            // Subscribe to new criteria
            if (_filterCriteria != null)
            {
                _filterCriteria.PropertyChanged += FilterCriteria_PropertyChanged;
                _filterCriteria.SelectedTags.CollectionChanged += SelectedTags_CollectionChanged;
            }

            UpdateUI();
        }

        private void FilterCriteria_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                UpdateUI();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateUI()
        {
            if (_filterCriteria == null) return;

            _isUpdating = true;
            try
            {
                // Update rating stars
                UpdateRatingStars(_filterCriteria.MinRating);

                // Update pick status buttons
                UpdatePickStatusButtons(_filterCriteria.PickStatus);

                // Update active filter indicator
                UpdateActiveFilterIndicator(_filterCriteria.IsActive);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateRatingStars(int? minRating)
        {
            var starIcons = new[] { MinStar1Icon, MinStar2Icon, MinStar3Icon, MinStar4Icon, MinStar5Icon };
            var yellowBrush = new SolidColorBrush(Color.FromRgb(255, 187, 36)); // #FFFBBF24
            var grayBrush = FindResource("MutedForegroundBrush") as SolidColorBrush;

            for (int i = 0; i < starIcons.Length; i++)
            {
                var icon = starIcons[i];
                if (icon != null)
                {
                    // Color the star yellow if it's within the minimum rating, gray otherwise
                    if (minRating.HasValue && minRating.Value > i)
                    {
                        icon.Foreground = yellowBrush;
                    }
                    else
                    {
                        icon.Foreground = grayBrush;
                    }
                }
            }
        }

        private void UpdatePickStatusButtons(bool? pickStatus)
        {
            var greenBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // #FF22C55E
            var redBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #FFEF4444
            var accentBrush = FindResource("AccentBrush") as SolidColorBrush;
            var grayBrush = FindResource("MutedForegroundBrush") as SolidColorBrush;

            // Update picked button (green flag)
            if (PickedFilterIcon != null)
            {
                PickedFilterIcon.Foreground = pickStatus == true ? greenBrush : grayBrush;
            }

            // Update rejected button (red cross)
            if (RejectedFilterIcon != null)
            {
                RejectedFilterIcon.Foreground = pickStatus == false ? redBrush : grayBrush;
            }

            // Update any button (accent asterisk)
            if (AnyPickFilterIcon != null)
            {
                AnyPickFilterIcon.Foreground = pickStatus == null ? accentBrush : grayBrush;
            }
        }

        private void UpdateActiveFilterIndicator(bool isActive)
        {
            if (ActiveFilterIndicator != null)
            {
                ActiveFilterIndicator.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Event Handlers

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filterCriteria != null)
            {
                _isUpdating = true;
                try
                {
                    _filterCriteria.Reset();

                    // Manually clear the search text box since it's two-way bound
                    SearchTextBox.Text = string.Empty;
                }
                finally
                {
                    _isUpdating = false;
                }

                UpdateUI();
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void MinRating_Click(object sender, RoutedEventArgs e)
        {
            if (_filterCriteria == null || sender is not Button button || button.Tag is not string starNumberStr)
                return;

            if (int.TryParse(starNumberStr, out int starNumber))
            {
                // If clicking the same star that's already selected, clear the rating filter
                if (_filterCriteria.MinRating == starNumber)
                {
                    _filterCriteria.MinRating = null;
                }
                else
                {
                    _filterCriteria.MinRating = starNumber;
                }
            }
        }

        private void PickFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_filterCriteria == null || sender is not Button button || button.Tag is not string pickType)
                return;

            bool? newPickValue = pickType switch
            {
                "Picked" => _filterCriteria.PickStatus == true ? null : true,
                "Rejected" => _filterCriteria.PickStatus == false ? null : false,
                "Any" => _filterCriteria.PickStatus == null ? null : null,
                _ => null
            };

            _filterCriteria.PickStatus = newPickValue;
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}