using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenFrame.Models;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;

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

            // Ensure templates are loaded before we try to update them
            this.Loaded += (s, e) => UpdateUI();
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
            var starIcons = new[] { Star1Icon, Star2Icon, Star3Icon, Star4Icon, Star5Icon };
            var yellowBrush = new SolidColorBrush(Color.FromRgb(255, 187, 36)); // #FFFBBF24
            var grayBrush = FindResource("MutedForegroundBrush") as SolidColorBrush;

            for (int i = 0; i < starIcons.Length; i++)
            {
                var icon = starIcons[i];
                if (icon != null)
                {
                    // Color the star yellow if it's within the rating, gray otherwise
                    if (rating.HasValue && rating.Value > i)
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

        private void UpdatePickStatus(bool? pick)
        {
            var greenBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // #FF22C55E
            var redBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #FFEF4444
            var grayBrush = FindResource("MutedForegroundBrush") as SolidColorBrush;

            // Update picked button (green flag)
            if (PickedIcon != null)
            {
                PickedIcon.Foreground = pick == true ? greenBrush : grayBrush;
            }

            // Update rejected button (red cross)
            if (RejectedIcon != null)
            {
                RejectedIcon.Foreground = pick == false ? redBrush : grayBrush;
            }
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

                if (button == PickedButton)
                {
                    // If already picked, clear it; otherwise set to picked
                    newPickValue = _userMetadata.Pick == true ? null : true;
                }
                else if (button == RejectedButton)
                {
                    // If already rejected, clear it; otherwise set to rejected
                    newPickValue = _userMetadata.Pick == false ? null : false;
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