using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenFrame.Controls
{
    /// <summary>
    /// GitLab-inspired tagging control for managing video tags
    /// </summary>
    public partial class TaggingControl : UserControl, INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty CurrentTagsProperty =
            DependencyProperty.Register(nameof(CurrentTags), typeof(ObservableCollection<Tag>), typeof(TaggingControl),
                new PropertyMetadata(new ObservableCollection<Tag>(), OnCurrentTagsChanged));

        public static readonly DependencyProperty AvailableTagsProperty =
            DependencyProperty.Register(nameof(AvailableTags), typeof(ObservableCollection<Tag>), typeof(TaggingControl),
                new PropertyMetadata(new ObservableCollection<Tag>(), OnAvailableTagsChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TaggingControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty AllowModifyAvailableTagsProperty =
            DependencyProperty.Register(nameof(AllowModifyAvailableTags), typeof(bool), typeof(TaggingControl),
                new PropertyMetadata(false, OnAllowModifyAvailableTagsChanged));

        #endregion

        #region Public Properties

        /// <summary>
        /// Collection of currently assigned tags
        /// </summary>
        public ObservableCollection<Tag> CurrentTags
        {
            get => (ObservableCollection<Tag>)GetValue(CurrentTagsProperty);
            set => SetValue(CurrentTagsProperty, value);
        }

        /// <summary>
        /// Collection of all available tags for selection
        /// </summary>
        public ObservableCollection<Tag> AvailableTags
        {
            get => (ObservableCollection<Tag>)GetValue(AvailableTagsProperty);
            set => SetValue(AvailableTagsProperty, value);
        }

        /// <summary>
        /// When true, only displays current tags without edit capabilities
        /// </summary>
        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        /// <summary>
        /// When true, allows creating new tags that are added to AvailableTags
        /// </summary>
        public bool AllowModifyAvailableTags
        {
            get => (bool)GetValue(AllowModifyAvailableTagsProperty);
            set => SetValue(AllowModifyAvailableTagsProperty, value);
        }

        #endregion

        #region Private Properties for Binding

        private ObservableCollection<Tag> _filteredAvailableTags;
        public ObservableCollection<Tag> FilteredAvailableTags
        {
            get => _filteredAvailableTags;
            set
            {
                _filteredAvailableTags = value;
                OnPropertyChanged();
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterAvailableTags();
            }
        }

        private string _newTagName;
        public string NewTagName
        {
            get => _newTagName;
            set
            {
                _newTagName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCreateTag));
                ValidateNewTagName();
            }
        }

        public bool HasCurrentTags => CurrentTags?.Count > 0;

        public bool HasSelectedAvailableTag => AvailableTagsComboBox?.SelectedItem != null;

        public bool CanCreateNewTags => !IsReadOnly && AllowModifyAvailableTags;

        public bool CanCreateTag => !string.IsNullOrWhiteSpace(NewTagName) && IsValidNewTagName(NewTagName);

        #endregion

        #region Events

        /// <summary>
        /// Fired when a tag is added to CurrentTags
        /// </summary>
        public event EventHandler<TagEventArgs> TagAdded;

        /// <summary>
        /// Fired when a tag is removed from CurrentTags
        /// </summary>
        public event EventHandler<TagEventArgs> TagRemoved;

        /// <summary>
        /// Fired when a new tag is created and added to AvailableTags
        /// </summary>
        public event EventHandler<TagEventArgs> TagCreated;

        /// <summary>
        /// Fired when CurrentTags collection changes
        /// </summary>
        public event EventHandler<TagCollectionChangedEventArgs> CurrentTagsChanged;

        #endregion

        #region Predefined Colors

        private static readonly Color[] TagColors = new[]
        {
            Color.FromRgb(255, 99, 132),   // Red
            Color.FromRgb(54, 162, 235),   // Blue
            Color.FromRgb(255, 205, 86),   // Yellow
            Color.FromRgb(75, 192, 192),   // Teal
            Color.FromRgb(153, 102, 255),  // Purple
            Color.FromRgb(255, 159, 64),   // Orange
            Color.FromRgb(199, 199, 199),  // Grey
            Color.FromRgb(83, 102, 255),   // Indigo
            Color.FromRgb(255, 99, 255),   // Pink
            Color.FromRgb(99, 255, 132),   // Green
            Color.FromRgb(255, 193, 7),    // Amber
            Color.FromRgb(76, 175, 80),    // Light Green
            Color.FromRgb(233, 30, 99),    // Pink
            Color.FromRgb(156, 39, 176),   // Deep Purple
            Color.FromRgb(63, 81, 181),    // Indigo
            Color.FromRgb(33, 150, 243),   // Light Blue
            Color.FromRgb(0, 188, 212),    // Cyan
            Color.FromRgb(0, 150, 136),    // Teal
            Color.FromRgb(139, 195, 74),   // Light Green
            Color.FromRgb(205, 220, 57)    // Lime
        };

        #endregion

        public TaggingControl()
        {
            InitializeComponent();

            // Initialize collections
            if (CurrentTags == null)
                CurrentTags = new ObservableCollection<Tag>();
            if (AvailableTags == null)
                AvailableTags = new ObservableCollection<Tag>();

            FilteredAvailableTags = new ObservableCollection<Tag>();

            // Set placeholder text
            AvailableTagsComboBox.Text = "Search or select tag...";
            NewTagNameTextBox.Text = "Enter new tag name";

            // Handle text box focus for placeholder behavior
            NewTagNameTextBox.GotFocus += (s, e) =>
            {
                if (NewTagNameTextBox.Text == "Enter new tag name")
                {
                    NewTagNameTextBox.Text = "";
                    NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("ForegroundBrush");
                }
            };

            NewTagNameTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(NewTagNameTextBox.Text))
                {
                    NewTagNameTextBox.Text = "Enter new tag name";
                    NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("MutedForegroundBrush");
                }
            };
        }

        #region Event Handlers

        private void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Tag tag)
            {
                RemoveCurrentTag(tag);
            }
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableTagsComboBox.SelectedItem is Tag selectedTag)
            {
                AddCurrentTag(selectedTag);
                ClearDropdownSelection();
            }
        }

        private void CreateTag_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTag();
        }

        private void AvailableTagsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasSelectedAvailableTag));
        }

        private void AvailableTagsComboBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow typing in the combo box for search
        }

        private void AvailableTagsComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && AvailableTagsComboBox.SelectedItem is Tag selectedTag)
            {
                AddCurrentTag(selectedTag);
                ClearDropdownSelection();
                e.Handled = true;
            }
        }

        private void NewTagNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && CanCreateTag)
            {
                CreateNewTag();
                e.Handled = true;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clears all current tags
        /// </summary>
        public void ClearCurrentTags()
        {
            if (IsReadOnly) return;

            var removedTags = CurrentTags.ToList();
            CurrentTags.Clear();

            foreach (var tag in removedTags)
            {
                TagRemoved?.Invoke(this, new TagEventArgs(tag));
            }
        }

        /// <summary>
        /// Adds a tag to current tags if not already present
        /// </summary>
        public void AddCurrentTag(Tag tag)
        {
            if (IsReadOnly || tag == null) return;

            if (!CurrentTags.Any(t => t.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)))
            {
                CurrentTags.Add(tag);
                TagAdded?.Invoke(this, new TagEventArgs(tag));
                FilterAvailableTags();
                OnPropertyChanged(nameof(HasCurrentTags));
            }
        }

        /// <summary>
        /// Removes a tag from current tags
        /// </summary>
        public void RemoveCurrentTag(Tag tag)
        {
            if (IsReadOnly || tag == null) return;

            var existingTag = CurrentTags.FirstOrDefault(t => t.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase));
            if (existingTag != null)
            {
                CurrentTags.Remove(existingTag);
                TagRemoved?.Invoke(this, new TagEventArgs(existingTag));
                FilterAvailableTags();
                OnPropertyChanged(nameof(HasCurrentTags));
            }
        }

        /// <summary>
        /// Creates a new tag and adds it to both AvailableTags and CurrentTags
        /// </summary>
        public void CreateNewTag()
        {
            if (!AllowModifyAvailableTags || string.IsNullOrWhiteSpace(NewTagName)) return;

            var tagName = NewTagName.Trim();
            if (!IsValidNewTagName(tagName)) return;

            var newTag = new Tag
            {
                Name = tagName,
                Color = GenerateRandomColor()
            };

            AvailableTags.Add(newTag);
            AddCurrentTag(newTag);

            TagCreated?.Invoke(this, new TagEventArgs(newTag));

            // Clear the input
            NewTagName = "";
            NewTagNameTextBox.Text = "Enter new tag name";
            NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("MutedForegroundBrush");

            ClearValidationMessage();
        }

        #endregion

        #region Private Methods

        private void FilterAvailableTags()
        {
            if (AvailableTags == null)
            {
                FilteredAvailableTags?.Clear();
                return;
            }

            var searchTerm = SearchText?.ToLower() ?? "";
            var currentTagNames = CurrentTags?.Select(t => t.Name.ToLower()).ToHashSet() ?? new HashSet<string>();

            var filteredTags = AvailableTags
                .Where(tag => !currentTagNames.Contains(tag.Name.ToLower()))
                .Where(tag => string.IsNullOrEmpty(searchTerm) || tag.Name.ToLower().Contains(searchTerm))
                .OrderBy(tag => tag.Name)
                .ToList();

            FilteredAvailableTags.Clear();
            foreach (var tag in filteredTags)
            {
                FilteredAvailableTags.Add(tag);
            }
        }

        private void ClearDropdownSelection()
        {
            AvailableTagsComboBox.SelectedItem = null;
            AvailableTagsComboBox.Text = "Search or select tag...";
            SearchText = "";
            OnPropertyChanged(nameof(HasSelectedAvailableTag));
        }

        private bool IsValidNewTagName(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return false;

            var trimmedName = tagName.Trim();

            // Check if tag already exists in available tags
            var nameExists = AvailableTags?.Any(t => t.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)) ?? false;

            return !nameExists && trimmedName.Length >= 2 && trimmedName.Length <= 50;
        }

        private void ValidateNewTagName()
        {
            if (string.IsNullOrWhiteSpace(NewTagName) || NewTagName == "Enter new tag name")
            {
                ClearValidationMessage();
                return;
            }

            var trimmedName = NewTagName.Trim();

            if (trimmedName.Length < 2)
            {
                ShowValidationMessage("Tag name must be at least 2 characters");
            }
            else if (trimmedName.Length > 50)
            {
                ShowValidationMessage("Tag name must be less than 50 characters");
            }
            else if (AvailableTags?.Any(t => t.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)) == true)
            {
                ShowValidationMessage("A tag with this name already exists");
            }
            else
            {
                ClearValidationMessage();
            }
        }

        private void ShowValidationMessage(string message)
        {
            ValidationMessageTextBlock.Text = message;
            ValidationMessageTextBlock.Visibility = Visibility.Visible;
        }

        private void ClearValidationMessage()
        {
            ValidationMessageTextBlock.Visibility = Visibility.Collapsed;
        }

        private string GenerateRandomColor()
        {
            var usedColors = AvailableTags?.Select(t => t.Color).ToHashSet() ?? new HashSet<string>();

            // Try to find an unused predefined color
            foreach (var color in TagColors)
            {
                var colorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                if (!usedColors.Contains(colorHex))
                {
                    return colorHex;
                }
            }

            // If all predefined colors are used, generate a random one
            var random = new Random();
            Color randomColor;
            string randomColorHex;

            do
            {
                randomColor = Color.FromRgb(
                    (byte)random.Next(100, 256), // Avoid too dark colors
                    (byte)random.Next(100, 256),
                    (byte)random.Next(100, 256)
                );
                randomColorHex = $"#{randomColor.R:X2}{randomColor.G:X2}{randomColor.B:X2}";
            } while (usedColors.Contains(randomColorHex));

            return randomColorHex;
        }

        #endregion

        #region Dependency Property Callbacks

        private static void OnCurrentTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TaggingControl control)
            {
                control.FilterAvailableTags();
                control.OnPropertyChanged(nameof(HasCurrentTags));

                if (e.OldValue is ObservableCollection<Tag> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.CurrentTags_CollectionChanged;
                }

                if (e.NewValue is ObservableCollection<Tag> newCollection)
                {
                    newCollection.CollectionChanged += control.CurrentTags_CollectionChanged;
                }

                control.CurrentTagsChanged?.Invoke(control, new TagCollectionChangedEventArgs(
                    e.OldValue as ObservableCollection<Tag>,
                    e.NewValue as ObservableCollection<Tag>
                ));
            }
        }

        private static void OnAvailableTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TaggingControl control)
            {
                control.FilterAvailableTags();

                if (e.OldValue is ObservableCollection<Tag> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.AvailableTags_CollectionChanged;
                }

                if (e.NewValue is ObservableCollection<Tag> newCollection)
                {
                    newCollection.CollectionChanged += control.AvailableTags_CollectionChanged;
                }
            }
        }

        private static void OnAllowModifyAvailableTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TaggingControl control)
            {
                control.OnPropertyChanged(nameof(CanCreateNewTags));
            }
        }

        private void CurrentTags_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            FilterAvailableTags();
            OnPropertyChanged(nameof(HasCurrentTags));
        }

        private void AvailableTags_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            FilterAvailableTags();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Event arguments for tag-related events
    /// </summary>
    public class TagEventArgs : EventArgs
    {
        public Tag Tag { get; }

        public TagEventArgs(Tag tag)
        {
            Tag = tag;
        }
    }

    /// <summary>
    /// Event arguments for tag collection changes
    /// </summary>
    public class TagCollectionChangedEventArgs : EventArgs
    {
        public ObservableCollection<Tag> OldCollection { get; }
        public ObservableCollection<Tag> NewCollection { get; }

        public TagCollectionChangedEventArgs(ObservableCollection<Tag> oldCollection, ObservableCollection<Tag> newCollection)
        {
            OldCollection = oldCollection;
            NewCollection = newCollection;
        }
    }

    #endregion
}