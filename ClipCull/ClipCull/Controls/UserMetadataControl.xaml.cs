﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipCull.Core;
using ClipCull.Models;

namespace ClipCull.Controls
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

        private ObservableCollection<Tag> AvailableTags;

        #endregion

        #region Constructor

        public UserMetadataControl()
        {
            InitializeComponent();

            RefreshAvailableTags();

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
            this.Loaded += UserMetadataControl_Loaded;

            //Subscribe Hotkeys
            HotkeyController.OnPick += HotkeyController_OnPick;
            HotkeyController.OnReject += HotkeyController_OnReject;
            HotkeyController.OnRemovePickReject += HotkeyController_OnRemovePickReject;
            HotkeyController.OnNumber1 += HotkeyController_OnNumber1;
            HotkeyController.OnNumber2 += HotkeyController_OnNumber2;
            HotkeyController.OnNumber3 += HotkeyController_OnNumber3;
            HotkeyController.OnNumber4 += HotkeyController_OnNumber4;
            HotkeyController.OnNumber5 += HotkeyController_OnNumber5;
            HotkeyController.OnNumber0 += HotkeyController_OnNumber0;
        }
        
        private void UserMetadataControl_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshAvailableTags();
            UpdateUI();
        }

        private void RefreshAvailableTags()
        {
            //Tags
            if (AvailableTags != null)
                AvailableTags.CollectionChanged -= AvailableTags_CollectionChanged;
            AvailableTags = new ObservableCollection<Tag>();
            SettingsHandler.Settings.Tags.ForEach(tag => AvailableTags.Add(tag));
            AvailableTags.CollectionChanged += AvailableTags_CollectionChanged;
        }

        private void AvailableTags_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //Lets try not to break the tags
            if (AvailableTags == null || AvailableTags.Count == 0)
                return;

            SettingsHandler.Settings.Tags = AvailableTags.ToList();
            SettingsHandler.Save();
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

                //Tags
                TagControl.CurrentTags = _userMetadata.Tags;
                TagControl.AvailableTags = AvailableTags;
                TagControl.AllowModifyAvailableTags = true;
                TagControl.IsReadOnly = false;

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
                TagControl.CurrentTags = new ObservableCollection<Tag>();

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
            _userMetadata.Tags = TagControl.CurrentTags ?? new ObservableCollection<Tag>();
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

        #region Hotkeys
        private void HotkeyController_OnRemovePickReject()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Pick = null;
                UpdatePickStatus(_userMetadata.Pick);
            }
        }

        private void HotkeyController_OnReject()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Pick = false;
                UpdatePickStatus(_userMetadata.Pick);
            }
        }

        private void HotkeyController_OnPick()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Pick = true;
                UpdatePickStatus(_userMetadata.Pick);
            }
        }
        private void HotkeyController_OnNumber0()
        {
            if (_userMetadata != null && IsVisible)  
            {
                _userMetadata.Rating = null;
                UpdateRatingStars(null);
            }
        }
        private void HotkeyController_OnNumber1()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Rating = 1;
                UpdateRatingStars(1);
            }
        }
        private void HotkeyController_OnNumber2()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Rating = 2;
                UpdateRatingStars(2);
            }
        }
        private void HotkeyController_OnNumber3()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Rating = 3;
                UpdateRatingStars(3);
            }
        }
        private void HotkeyController_OnNumber4()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Rating = 4;
                UpdateRatingStars(4);
            }
        }
        private void HotkeyController_OnNumber5()
        {
            if (_userMetadata != null && IsVisible)
            {
                _userMetadata.Rating = 5;
                UpdateRatingStars(5);
            }
        }
        #endregion

        #endregion

        #region INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}