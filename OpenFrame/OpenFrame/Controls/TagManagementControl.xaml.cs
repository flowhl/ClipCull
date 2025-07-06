using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using OpenFrame.Core;
using OpenFrame.Models;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace OpenFrame.Controls
{
    /// <summary>
    /// Compact tag management control for settings/admin interface
    /// </summary>
    public partial class TagManagementControl : UserControl, INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty TagsProperty =
            DependencyProperty.Register(nameof(Tags), typeof(ObservableCollection<EditableTag>), typeof(TagManagementControl),
                new PropertyMetadata(new ObservableCollection<EditableTag>(), OnTagsChanged));

        public static readonly DependencyProperty AllowDuplicateNamesProperty =
            DependencyProperty.Register(nameof(AllowDuplicateNames), typeof(bool), typeof(TagManagementControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty MaxTagNameLengthProperty =
            DependencyProperty.Register(nameof(MaxTagNameLength), typeof(int), typeof(TagManagementControl),
                new PropertyMetadata(50));

        #endregion

        #region Public Properties

        /// <summary>
        /// Collection of tags to manage
        /// </summary>
        public ObservableCollection<EditableTag> Tags
        {
            get => (ObservableCollection<EditableTag>)GetValue(TagsProperty);
            set => SetValue(TagsProperty, value);
        }

        /// <summary>
        /// Whether to allow duplicate tag names
        /// </summary>
        public bool AllowDuplicateNames
        {
            get => (bool)GetValue(AllowDuplicateNamesProperty);
            set => SetValue(AllowDuplicateNamesProperty, value);
        }

        /// <summary>
        /// Maximum length for tag names
        /// </summary>
        public int MaxTagNameLength
        {
            get => (int)GetValue(MaxTagNameLengthProperty);
            set => SetValue(MaxTagNameLengthProperty, value);
        }

        #endregion

        #region Private Properties for Binding

        private string _newTagName;
        public string NewTagName
        {
            get => _newTagName;
            set
            {
                _newTagName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAddNewTag));
                ValidateNewTagName();
            }
        }

        public bool CanAddNewTag => !string.IsNullOrWhiteSpace(NewTagName) && IsValidNewTagName(NewTagName);

        public bool HasSelectedTags => TagsListView?.SelectedItems?.Count > 0;

        public int TotalTagCount => Tags?.Count ?? 0;

        public int SelectedTagCount => TagsListView?.SelectedItems?.Count ?? 0;

        private EditableTag _currentColorEditTag;

        #endregion

        #region Events

        /// <summary>
        /// Fired when a tag is added
        /// </summary>
        public event EventHandler<TagEventArgs> TagAdded;

        /// <summary>
        /// Fired when a tag is deleted
        /// </summary>
        public event EventHandler<TagEventArgs> TagDeleted;

        /// <summary>
        /// Fired when a tag is modified
        /// </summary>
        public event EventHandler<TagEventArgs> TagModified;

        /// <summary>
        /// Fired when tags are imported
        /// </summary>
        public event EventHandler<TagImportEventArgs> TagsImported;

        #endregion

        #region Predefined Colors

        private static readonly Color[] PredefinedColors = new[]
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

        public TagManagementControl()
        {
            InitializeComponent();

            if (Tags == null)
                Tags = new ObservableCollection<EditableTag>();

            SetupColorPicker();
            SetupPlaceholderText();
        }

        #region Event Handlers

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            AddNewTag();
        }

        private void NewTagNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && CanAddNewTag)
            {
                AddNewTag();
                e.Handled = true;
            }
        }

        private void TagName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) // Double-click to edit
            {
                if (sender is FrameworkElement element && element.DataContext is EditableTag tag)
                {
                    StartEditing(tag);
                }
            }
        }

        private void TagNameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is EditableTag tag)
            {
                FinishEditing(tag, textBox.Text);
            }
        }

        private void TagNameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is EditableTag tag)
            {
                if (e.Key == Key.Enter)
                {
                    FinishEditing(tag, textBox.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelEditing(tag);
                    e.Handled = true;
                }
            }
        }

        private void TagNameEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void ColorDot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is EditableTag tag)
            {
                _currentColorEditTag = tag;
                ColorPickerPopup.IsOpen = true;
            }
        }

        private void DeleteSingle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is EditableTag tag)
            {
                DeleteTag(tag);
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedTags();
        }

        private void RandomizeColors_Click(object sender, RoutedEventArgs e)
        {
            RandomizeSelectedTagColors();
        }

        private void ExportTags_Click(object sender, RoutedEventArgs e)
        {
            ExportTags();
        }

        private void ImportTags_Click(object sender, RoutedEventArgs e)
        {
            ImportTags();
        }

        private void TagsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasSelectedTags));
            OnPropertyChanged(nameof(SelectedTagCount));
        }

        private void TagsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && HasSelectedTags)
            {
                DeleteSelectedTags();
                e.Handled = true;
            }
            else if (e.Key == Key.F2 && TagsListView.SelectedItem is EditableTag tag)
            {
                StartEditing(tag);
                e.Handled = true;
            }
        }

        private void RandomColor_Click(object sender, RoutedEventArgs e)
        {
            if (_currentColorEditTag != null)
            {
                var oldColor = _currentColorEditTag.Color;
                _currentColorEditTag.Color = GenerateRandomColor();
                ColorPickerPopup.IsOpen = false;

                TagModified?.Invoke(this, new TagEventArgs(ConvertToTag(_currentColorEditTag)));
                _currentColorEditTag = null;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a new tag with the specified name and random color
        /// </summary>
        public void AddTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var trimmedName = name.Trim();
            if (!IsValidNewTagName(trimmedName)) return;

            var newTag = new EditableTag
            {
                Name = trimmedName,
                Color = GenerateRandomColor(),
                IsEditing = false
            };

            Tags.Add(newTag);
            TagAdded?.Invoke(this, new TagEventArgs(ConvertToTag(newTag)));

            OnPropertyChanged(nameof(TotalTagCount));
        }

        /// <summary>
        /// Deletes the specified tag
        /// </summary>
        public void DeleteTag(EditableTag tag)
        {
            if (tag == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the tag '{tag.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Tags.Remove(tag);
                TagDeleted?.Invoke(this, new TagEventArgs(ConvertToTag(tag)));
                OnPropertyChanged(nameof(TotalTagCount));
            }
        }

        /// <summary>
        /// Clears all tags with confirmation
        /// </summary>
        public void ClearAllTags()
        {
            if (Tags.Count == 0) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete all {Tags.Count} tags?",
                "Confirm Clear All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var deletedTags = Tags.ToList();
                Tags.Clear();

                foreach (var tag in deletedTags)
                {
                    TagDeleted?.Invoke(this, new TagEventArgs(ConvertToTag(tag)));
                }

                OnPropertyChanged(nameof(TotalTagCount));
            }
        }

        #endregion

        #region Private Methods

        private void SetupColorPicker()
        {
            foreach (var color in PredefinedColors)
            {
                var button = new Button
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(color),
                    BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    Tag = color
                };

                button.Click += ColorPickerButton_Click;
                ColorGrid.Children.Add(button);
            }
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Color color && _currentColorEditTag != null)
            {
                var oldColor = _currentColorEditTag.Color;
                _currentColorEditTag.Color = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                ColorPickerPopup.IsOpen = false;

                TagModified?.Invoke(this, new TagEventArgs(ConvertToTag(_currentColorEditTag)));
                _currentColorEditTag = null;
            }
        }

        private void SetupPlaceholderText()
        {
            NewTagNameTextBox.Text = "Enter tag name";
            NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("MutedForegroundBrush");

            NewTagNameTextBox.GotFocus += (s, e) =>
            {
                if (NewTagNameTextBox.Text == "Enter tag name")
                {
                    NewTagNameTextBox.Text = "";
                    NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("ForegroundBrush");
                }
            };

            NewTagNameTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(NewTagNameTextBox.Text))
                {
                    NewTagNameTextBox.Text = "Enter tag name";
                    NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("MutedForegroundBrush");
                }
            };
        }

        private void AddNewTag()
        {
            if (!CanAddNewTag) return;

            AddTag(NewTagName);

            // Clear input
            NewTagName = "";
            NewTagNameTextBox.Text = "Enter tag name";
            NewTagNameTextBox.Foreground = (SolidColorBrush)FindResource("MutedForegroundBrush");
            ClearValidationMessage();
        }

        private void StartEditing(EditableTag tag)
        {
            // Cancel any other editing
            foreach (var t in Tags)
                t.IsEditing = false;

            tag.IsEditing = true;
            tag.OriginalName = tag.Name; // Store for cancel
        }

        private void FinishEditing(EditableTag tag, string newName)
        {
            tag.IsEditing = false;

            var trimmedName = newName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                // Revert to original name
                tag.Name = tag.OriginalName;
                return;
            }

            if (!IsValidEditTagName(trimmedName, tag))
            {
                // Show error and revert
                MessageBox.Show("Invalid tag name. Names must be unique and within length limits.",
                               "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                tag.Name = tag.OriginalName;
                return;
            }

            var oldName = tag.OriginalName;
            tag.Name = trimmedName;

            TagModified?.Invoke(this, new TagEventArgs(ConvertToTag(tag)));
        }

        private void CancelEditing(EditableTag tag)
        {
            tag.IsEditing = false;
            tag.Name = tag.OriginalName; // Revert changes
        }

        private void DeleteSelectedTags()
        {
            var selectedTags = TagsListView.SelectedItems.Cast<EditableTag>().ToList();
            if (selectedTags.Count == 0) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete {selectedTags.Count} selected tag(s)?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var tag in selectedTags)
                {
                    Tags.Remove(tag);
                    TagDeleted?.Invoke(this, new TagEventArgs(ConvertToTag(tag)));
                }

                OnPropertyChanged(nameof(TotalTagCount));
                OnPropertyChanged(nameof(HasSelectedTags));
                OnPropertyChanged(nameof(SelectedTagCount));
            }
        }

        private void RandomizeSelectedTagColors()
        {
            var selectedTags = TagsListView.SelectedItems.Cast<EditableTag>().ToList();
            if (selectedTags.Count == 0) return;

            foreach (var tag in selectedTags)
            {
                tag.Color = GenerateRandomColor();
                TagModified?.Invoke(this, new TagEventArgs(ConvertToTag(tag)));
            }
        }

        private void ExportTags()
        {
            if (Tags.Count == 0)
            {
                MessageBox.Show("No tags to export.", "Export Tags",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string saveFile = DialogHelper.SaveFile("Export Tags", "json", null, "tags_export.json");

            if (saveFile == null)
                return;
            try
            {
                var exportData = Tags.Select(t => new { t.Name, t.Color }).ToList();
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(saveFile, json);

                Logger.LogSuccess($"Successfully exported {Tags.Count} tags to {saveFile}", "Export Complete");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to export tags: {ex.Message}", ex, "Export Error");
            }
        }


        private void ImportTags()
        {
            string jsonPath = DialogHelper.ChooseFile("Select tags json file", "JSON files (*.json)|*.json|All files (*.*)|*.*");
            try
            {
                var json = File.ReadAllText(jsonPath);
                var importData = JsonSerializer.Deserialize<List<JsonElement>>(json);

                var importedTags = new List<EditableTag>();
                var skippedCount = 0;

                foreach (var item in importData)
                {
                    if (item.TryGetProperty("Name", out var nameElement) &&
                        item.TryGetProperty("Color", out var colorElement))
                    {
                        var name = nameElement.GetString()?.Trim();
                        var color = colorElement.GetString()?.Trim();

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(color))
                        {
                            // Check if tag already exists
                            if (!AllowDuplicateNames && Tags.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                skippedCount++;
                                continue;
                            }

                            var newTag = new EditableTag
                            {
                                Name = name,
                                Color = color,
                                IsEditing = false
                            };

                            importedTags.Add(newTag);
                            Tags.Add(newTag);
                        }
                    }
                }

                string message = $"Successfully imported {importedTags.Count} tags.";
                if (skippedCount > 0)
                    message += $" Skipped {skippedCount} duplicate tags.";

                Logger.LogSuccess(message, "Import Successfull");

                if (importedTags.Count > 0)
                {
                    TagsImported?.Invoke(this, new TagImportEventArgs(importedTags.Select(ConvertToTag).ToList()));
                    OnPropertyChanged(nameof(TotalTagCount));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to import tags: {ex.Message}", ex, "Import Error");
            }

        }

        private bool IsValidNewTagName(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return false;

            var trimmedName = tagName.Trim();

            if (trimmedName.Length < 1 || trimmedName.Length > MaxTagNameLength)
                return false;

            if (!AllowDuplicateNames && Tags.Any(t => t.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        private bool IsValidEditTagName(string tagName, EditableTag currentTag)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return false;

            var trimmedName = tagName.Trim();

            if (trimmedName.Length < 1 || trimmedName.Length > MaxTagNameLength)
                return false;

            if (!AllowDuplicateNames &&
                Tags.Any(t => t != currentTag && t.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        private void ValidateNewTagName()
        {
            if (string.IsNullOrWhiteSpace(NewTagName) || NewTagName == "Enter tag name")
            {
                ClearValidationMessage();
                return;
            }

            var trimmedName = NewTagName.Trim();

            if (trimmedName.Length > MaxTagNameLength)
            {
                ShowValidationMessage($"Tag name must be less than {MaxTagNameLength} characters");
            }
            else if (!AllowDuplicateNames && Tags.Any(t => t.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
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
            AddValidationMessage.Text = message;
            AddValidationMessage.Visibility = Visibility.Visible;
        }

        private void ClearValidationMessage()
        {
            AddValidationMessage.Visibility = Visibility.Collapsed;
        }

        private string GenerateRandomColor()
        {
            var usedColors = Tags.Select(t => t.Color).ToHashSet();

            // Try predefined colors first
            foreach (var color in PredefinedColors)
            {
                var colorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                if (!usedColors.Contains(colorHex))
                {
                    return colorHex;
                }
            }

            // Generate random color if all predefined are used
            var random = new Random();
            Color randomColor;
            string randomColorHex;

            do
            {
                randomColor = Color.FromRgb(
                    (byte)random.Next(100, 256),
                    (byte)random.Next(100, 256),
                    (byte)random.Next(100, 256)
                );
                randomColorHex = $"#{randomColor.R:X2}{randomColor.G:X2}{randomColor.B:X2}";
            } while (usedColors.Contains(randomColorHex));

            return randomColorHex;
        }

        private Tag ConvertToTag(EditableTag editableTag)
        {
            return new Tag
            {
                Name = editableTag.Name,
                Color = editableTag.Color
            };
        }

        #endregion

        #region Dependency Property Callbacks

        private static void OnTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TagManagementControl control)
            {
                control.OnPropertyChanged(nameof(TotalTagCount));

                if (e.OldValue is ObservableCollection<EditableTag> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.Tags_CollectionChanged;
                }

                if (e.NewValue is ObservableCollection<EditableTag> newCollection)
                {
                    newCollection.CollectionChanged += control.Tags_CollectionChanged;
                }
            }
        }

        private void Tags_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(TotalTagCount));
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
    /// Editable tag model for management interface
    /// </summary>
    public class EditableTag : INotifyPropertyChanged
    {
        private string _name;
        private string _color;
        private bool _isEditing;

        /// <summary>
        /// Tag name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tag color in hex format
        /// </summary>
        public string Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ColorValue));
            }
        }

        /// <summary>
        /// Whether this tag is currently being edited
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Original name for cancel operations
        /// </summary>
        public string OriginalName { get; set; }

        /// <summary>
        /// Helper property for color binding
        /// </summary>
        public Color ColorValue
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(Color)) return Colors.Gray;
                    var colorString = Color.StartsWith("#") ? Color : $"#{Color}";
                    return (Color)ColorConverter.ConvertFromString(colorString);
                }
                catch
                {
                    return Colors.Gray;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Event arguments for tag import events
    /// </summary>
    public class TagImportEventArgs : EventArgs
    {
        public List<Tag> ImportedTags { get; }

        public TagImportEventArgs(List<Tag> importedTags)
        {
            ImportedTags = importedTags;
        }
    }

    #endregion
}