using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Linq;
using ClipCull.Controls;
using ClipCull.Models;

namespace ClipCull.Core
{
    public static class HotkeyController
    {
        private static Dictionary<string, HotkeyAction> _hotkeyActions = new Dictionary<string, HotkeyAction>();
        private static Dictionary<string, string> _hotkeyMappings = new Dictionary<string, string>();

        // Events for different actions
        public static event Action OnSave;
        public static event Action OnOpen;
        public static event Action OnUndo;
        public static event Action OnRedo;
        public static event Action OnTogglePlay;
        public static event Action OnNext;
        public static event Action OnPrevious;
        public static event Action OnNextSmall;
        public static event Action OnPreviousSmall;
        public static event Action OnSetInPoint;
        public static event Action OnSetOutPoint;
        public static event Action OnReload;
        public static event Action OnEnter;
        public static event Action OnMarker;
        public static event Action OnSubclipStart;
        public static event Action OnSubclipEnd;

        static HotkeyController()
        {
            // Register all available actions
            RegisterAction("Save", () => OnSave?.Invoke());
            RegisterAction("Open", () => OnOpen?.Invoke());
            RegisterAction("Undo", () => OnUndo?.Invoke());
            RegisterAction("Redo", () => OnRedo?.Invoke());
            RegisterAction("TogglePlay", () => OnTogglePlay?.Invoke());
            RegisterAction("Next", () => OnNext?.Invoke());
            RegisterAction("Previous", () => OnPrevious?.Invoke());
            RegisterAction("SetInPoint", () => OnSetInPoint?.Invoke());
            RegisterAction("SetOutPoint", () => OnSetOutPoint?.Invoke());
            RegisterAction("Reload", () => OnReload?.Invoke());
            RegisterAction("Enter", () => OnEnter?.Invoke());
            RegisterAction("Marker", () => OnMarker?.Invoke());
            RegisterAction("SubclipStart", () => OnSubclipStart?.Invoke());
            RegisterAction("SubclipEnd", () => OnSubclipEnd?.Invoke());
            RegisterAction("NextSmall", () => OnNextSmall?.Invoke());
            RegisterAction("PreviousSmall", () => OnPreviousSmall?.Invoke());

            // Load default mappings
            LoadDefaultMappings();
        }

        private static void RegisterAction(string actionName, Action action)
        {
            _hotkeyActions[actionName] = new HotkeyAction { Name = actionName, Action = action };
        }

        private static void LoadDefaultMappings()
        {
            _hotkeyMappings = GetDefaultMappings();
        }

        private static Dictionary<string, string> GetDefaultMappings()
        {
            var defaultMappings = new Dictionary<string, string>();

            // Default hotkey mappings
            defaultMappings["Ctrl+S"] = "Save";
            defaultMappings["Ctrl+O"] = "Open";
            defaultMappings["Ctrl+Z"] = "Undo";
            defaultMappings["Ctrl+Y"] = "Redo";
            defaultMappings["Space"] = "TogglePlay";
            defaultMappings["Right"] = "Next";
            defaultMappings["Left"] = "Previous";
            defaultMappings["Shift+Right"] = "NextSmall";
            defaultMappings["Shift+Left"] = "PreviousSmall";
            defaultMappings["I"] = "SetInPoint";
            defaultMappings["O"] = "SetOutPoint";
            defaultMappings["Ctrl+R"] = "Reload";
            defaultMappings["F5"] = "Reload";
            defaultMappings["Enter"] = "Enter";
            defaultMappings["M"] = "Marker";
            defaultMappings["Q"] = "SubclipStart";
            defaultMappings["E"] = "SubclipEnd";

            return defaultMappings;
        }

        /// <summary>
        /// Places the not existing default mappings into the settings.
        /// </summary>
        private static void MigrateMappings()
        {
            var currentMappings = SettingsHandler.Settings.HotkeyMappings ?? new List<HotkeyMapping>();
            foreach (var mapping in GetDefaultMappings())
            {
                if (!currentMappings.Any(m => m.Hotkey == mapping.Key && m.Action == mapping.Value))
                {
                    currentMappings.Add(new HotkeyMapping(mapping.Key, mapping.Value));
                }
            }
            SettingsHandler.Settings.HotkeyMappings = currentMappings;
            SettingsHandler.Save();
        }

        public static void ProcessKeyDown(KeyEventArgs e)
        {
            string hotkeyString = GetHotkeyString(e);

            if (_hotkeyMappings.TryGetValue(hotkeyString, out string actionName))
            {
                if (_hotkeyActions.TryGetValue(actionName, out HotkeyAction action))
                {
                    action.Action?.Invoke();
                    e.Handled = true;
                }
            }
        }

        private static string GetHotkeyString(KeyEventArgs e)
        {
            var modifiers = new List<string>();

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers.Add("Shift");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers.Add("Alt");

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (modifiers.Any())
                return string.Join("+", modifiers) + "+" + key.ToString();
            else
                return key.ToString();
        }

        public static Dictionary<string, string> GetCurrentMappings()
        {
            return new Dictionary<string, string>(_hotkeyMappings);
        }

        public static List<string> GetAvailableActions()
        {
            return _hotkeyActions.Keys.ToList();
        }

        public static void SetMapping(string hotkey, string actionName)
        {
            // Remove any existing mapping for this hotkey
            _hotkeyMappings.Remove(hotkey);

            // Remove any existing hotkey for this action
            var existingHotkey = _hotkeyMappings.FirstOrDefault(x => x.Value == actionName).Key;
            if (existingHotkey != null)
                _hotkeyMappings.Remove(existingHotkey);

            // Set new mapping
            if (!string.IsNullOrEmpty(actionName))
                _hotkeyMappings[hotkey] = actionName;
        }

        public static void RemoveMapping(string hotkey)
        {
            _hotkeyMappings.Remove(hotkey);
        }

        public static void SaveMappings()
        {
            // Convert dictionary to list for XML serialization
            var mappingsList = _hotkeyMappings.Select(kvp => new HotkeyMapping
            {
                Hotkey = kvp.Key,
                Action = kvp.Value
            }).ToList();

            SettingsHandler.Settings.HotkeyMappings = mappingsList;
            SettingsHandler.Save();
        }

        public static void LoadMappings()
        {
            // Load from settings and convert list back to dictionary
            if (SettingsHandler.Settings.HotkeyMappings != null)
            {
                _hotkeyMappings = SettingsHandler.Settings.HotkeyMappings
                    .ToDictionary(m => m.Hotkey, m => m.Action);
            }
            // Ensure default mappings are present
            MigrateMappings();
        }

        private class HotkeyAction
        {
            public string Name { get; set; }
            public Action Action { get; set; }
        }
    }
}