using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using OpenFrame.Models;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;

namespace OpenFrame.Controls
{
    /// <summary>
    /// UserControl for editing user metadata content
    /// </summary>
    public partial class UserMetadataControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private UserMetadataContent _userMetadata;
        private bool _isUpdating = false;
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty UserMetadataProperty =
            DependencyProperty.Register(nameof(UserMetadata), typeof(UserMetadataContent), typeof(UserMetadataControl),
                new PropertyMetadata(null, OnUserMetadataChanged));

        public UserMetadataContent UserMetadata
        {
            get => (UserMetadataContent)GetValue(UserMetadataProperty);
            set => SetValue(UserMetadataProperty, value);
        }

        private static void OnUserMetadataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UserMetadataControl control)
            {
                control.OnUserMetadataChanged((UserMetadataContent)e.OldValue, (UserMetadataContent)e.NewValue);
            }
        }

        #endregion

        #region Constructor

        public UserMetadataControl()
        {
            InitializeComponent();
            DataContext = this;

            // Wire up text change events
            TitleTextBox.TextChanged += OnTextChanged;
            DescriptionTextBox.TextChanged += OnTextChanged;
            AuthorTextBox.TextChanged += OnTextChanged;
            LocationTextBox.TextChanged += OnTextChanged;
            ReelTextBox.TextChanged += OnTextChanged;
            ShotTextBox.TextChanged += OnTextChanged;
            CameraTextBox.TextChanged += OnTextChanged;
            TagsTextBox.TextChanged += OnTextChanged;
            UpdateUI();
        }

        #endregion

        #region Private Methods

        private void OnUserMetadataChanged(UserMetadataContent oldValue, UserMetadataContent newValue)
        {
            _userMetadata = newValue;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_userMetadata == null)
            {
                ClearAllFields();
                svMain.Visibility = Visibility.Collapsed;
                return;
            }

            svMain.Visibility = Visibility.Visible;
            _isUpdating = true;
            try
            {
                // Update text fields
                TitleTextBox.Text = _userMetadata.Title ?? string.Empty;
                DescriptionTextBox.Text = _userMetadata.Description ?? string.Empty;
                AuthorTextBox.Text = _userMetadata.Author ?? string.Empty;
                LocationTextBox.Text = _userMetadata.Location ?? string.Empty;
                ReelTextBox.Text = _userMetadata.Reel ?? string.Empty;
                ShotTextBox.Text = _userMetadata.Shot ?? string.Empty;
                CameraTextBox.Text = _userMetadata.Camera ?? string.Empty;

                // Update tags
                TagsTextBox.Text = _userMetadata.Tags != null ? string.Join(", ", _userMetadata.Tags) : string.Empty;

                // Update rating stars
                UpdateRatingStars(_userMetadata.Rating);

                // Update pick status
                UpdatePickStatus(_userMetadata.Pick);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateRatingStars(int? rating)
        {
            var stars = new[] { Star1, Star2, Star3, Star4, Star5 };

            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].Tag = rating.HasValue && rating.Value > i;
            }
        }

        private void UpdatePickStatus(bool? pick)
        {
            PickedButton.Tag = pick == true;
            RejectedButton.Tag = pick == false;
            NoneButton.Tag = pick == null;
        }

        private void ClearAllFields()
        {
            _isUpdating = true;
            try
            {
                TitleTextBox.Text = string.Empty;
                DescriptionTextBox.Text = string.Empty;
                AuthorTextBox.Text = string.Empty;
                LocationTextBox.Text = string.Empty;
                ReelTextBox.Text = string.Empty;
                ShotTextBox.Text = string.Empty;
                CameraTextBox.Text = string.Empty;
                TagsTextBox.Text = string.Empty;

                UpdateRatingStars(null);
                UpdatePickStatus(null);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateModelFromUI()
        {
            if (_userMetadata == null || _isUpdating)
                return;

            _userMetadata.Title = string.IsNullOrWhiteSpace(TitleTextBox.Text) ? null : TitleTextBox.Text.Trim();
            _userMetadata.Description = string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? null : DescriptionTextBox.Text.Trim();
            _userMetadata.Author = string.IsNullOrWhiteSpace(AuthorTextBox.Text) ? null : AuthorTextBox.Text.Trim();
            _userMetadata.Location = string.IsNullOrWhiteSpace(LocationTextBox.Text) ? null : LocationTextBox.Text.Trim();
            _userMetadata.Reel = string.IsNullOrWhiteSpace(ReelTextBox.Text) ? null : ReelTextBox.Text.Trim();
            _userMetadata.Shot = string.IsNullOrWhiteSpace(ShotTextBox.Text) ? null : ShotTextBox.Text.Trim();
            _userMetadata.Camera = string.IsNullOrWhiteSpace(CameraTextBox.Text) ? null : CameraTextBox.Text.Trim();

            // Parse tags
            var tagsText = TagsTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(tagsText))
            {
                _userMetadata.Tags = null;
            }
            else
            {
                _userMetadata.Tags = tagsText
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(tag => tag.Trim())
                    .Where(tag => !string.IsNullOrEmpty(tag))
                    .ToList();

                if (_userMetadata.Tags.Count == 0)
                    _userMetadata.Tags = null;
            }
        }

        #endregion

        #region Event Handlers

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModelFromUI();
        }

        private void Star_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string starNumberStr)
            {
                if (int.TryParse(starNumberStr, out int starNumber))
                {
                    if (_userMetadata != null)
                    {
                        // If clicking the same star that's already selected, clear the rating
                        if (_userMetadata.Rating == starNumber)
                        {
                            _userMetadata.Rating = null;
                        }
                        else
                        {
                            _userMetadata.Rating = starNumber;
                        }

                        UpdateRatingStars(_userMetadata.Rating);
                    }
                }
            }
        }

        private void Pick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && _userMetadata != null)
            {
                bool? newPickValue = null;

                if (button.Tag is bool boolValue)
                {
                    // If clicking the same pick status that's already selected, clear it
                    if (_userMetadata.Pick == boolValue)
                    {
                        newPickValue = null;
                    }
                    else
                    {
                        newPickValue = boolValue;
                    }
                }
                else
                {
                    // This is the "None" button
                    newPickValue = null;
                }

                _userMetadata.Pick = newPickValue;
                UpdatePickStatus(_userMetadata.Pick);
            }
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