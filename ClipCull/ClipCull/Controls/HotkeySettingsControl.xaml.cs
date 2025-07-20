using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipCull.Core;
using ClipCull.Models;

namespace ClipCull.Controls
{
    public partial class HotkeySettingsControl : UserControl
    {
        private ObservableCollection<HotkeyMappingViewModel> _mappings;
        private TextBox _activeTextBox;

        public HotkeySettingsControl()
        {
            InitializeComponent();
            LoadHotkeyMappings();
        }

        private void LoadHotkeyMappings()
        {
            _mappings = new ObservableCollection<HotkeyMappingViewModel>();

            var currentMappings = HotkeyController.GetCurrentMappings();
            var availableActions = HotkeyController.GetAvailableActions();

            foreach (var action in availableActions)
            {
                var hotkey = currentMappings.FirstOrDefault(x => x.Value == action).Key ?? "";
                _mappings.Add(new HotkeyMappingViewModel
                {
                    ActionName = GetFriendlyActionName(action),
                    ActionKey = action,
                    Hotkey = hotkey
                });
            }

            HotkeyListView.ItemsSource = _mappings;
        }

        private string GetFriendlyActionName(string action)
        {
            // Convert action names to user-friendly display names
            switch (action)
            {
                case "Save": return "Save";
                case "Open": return "Open File";
                case "New": return "New Project";
                case "Copy": return "Copy";
                case "Paste": return "Paste";
                case "Cut": return "Cut";
                case "Undo": return "Undo";
                case "Redo": return "Redo";
                case "Export": return "Export Video";
                case "TogglePlay": return "Play/Pause";
                case "Next": return "Next / Skip 10s";
                case "Previous": return "Previous / Jump back 10s";
                case "SetInPoint": return "Set In Point";
                case "SetOutPoint": return "Set Out Point";
                case "Reload": return "Reload";
                case "Enter": return "Enter / Select";
                case "Marker": return "Add Marker";
                case "SubclipStart": return "Start Subclip";
                case "SubclipEnd": return "End Subclip";
                case "NextSmall": return "Next Frame";
                case "PreviousSmall": return "Previous Frame";
                default: return action;
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_activeTextBox == null) return;

            e.Handled = true;

            // Get the hotkey string
            var hotkeyString = GetHotkeyString(e);

            // Update the textbox
            _activeTextBox.Text = hotkeyString;

            // Update the mapping
            var mapping = _activeTextBox.Tag as HotkeyMappingViewModel;
            if (mapping != null)
            {
                // Check if this hotkey is already used
                var existingMapping = _mappings.FirstOrDefault(m => m.Hotkey == hotkeyString && m != mapping);
                if (existingMapping != null)
                {
                    MessageBox.Show($"This hotkey is already assigned to '{existingMapping.ActionName}'",
                        "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _activeTextBox.Text = mapping.Hotkey;
                    return;
                }

                mapping.Hotkey = hotkeyString;
                HotkeyController.SetMapping(hotkeyString, mapping.ActionKey);
                HotkeyController.SaveMappings();
            }
        }

        private string GetHotkeyString(KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore modifier keys alone
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                return "";
            }

            var modifiers = new System.Collections.Generic.List<string>();

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers.Add("Shift");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers.Add("Alt");

            if (modifiers.Any())
                return string.Join("+", modifiers) + "+" + key.ToString();
            else
                return key.ToString();
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _activeTextBox = sender as TextBox;
            if (_activeTextBox != null)
            {
                _activeTextBox.Background = System.Windows.Media.Brushes.LightBlue;
                _activeTextBox.Text = "Press keys...";
            }
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                textBox.SetResourceReference(TextBox.BackgroundProperty, "BackgroundBrush");
                var mapping = textBox.Tag as HotkeyMapping;
                if (mapping != null && textBox.Text == "Press keys...")
                {
                    textBox.Text = mapping.Hotkey;
                }
            }
            _activeTextBox = null;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var mapping = button?.Tag as HotkeyMappingViewModel;
            if (mapping != null)
            {
                HotkeyController.RemoveMapping(mapping.Hotkey);
                mapping.Hotkey = "";
                HotkeyController.SaveMappings();
            }
        }
    }

    public class HotkeyMappingViewModel : INotifyPropertyChanged
    {
        private string _hotkey;

        public string ActionName { get; set; }
        public string ActionKey { get; set; }

        public string Hotkey
        {
            get => _hotkey;
            set
            {
                _hotkey = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}