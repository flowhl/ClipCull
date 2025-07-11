using ClipCull.Models;
using ClipCull.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Layout.Serialization;

namespace ClipCull.Core
{
    /// <summary>
    /// Simplified layout manager that works with AvalonDockLayoutSerializer attached behavior
    /// </summary>
    public static class LayoutManager
    {
        private static readonly string SettingsPath = Path.Combine(Globals.SettingsPath, "layout.xml");
        private static WindowSettings _windowSettings;

        public static WindowSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return Globals.DeserializeFromFile<WindowSettings>(SettingsPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load layout: {ex.Message}");
            }
            return new WindowSettings(); // Return defaults
        }

        public static void SaveSettings(WindowSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                Globals.SerializeToFile(settings, SettingsPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save layout: {ex.Message}");
            }
        }

        public static void ResetLayout(string dockManagerName = null)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var settings = LoadSettings();

                    if (string.IsNullOrEmpty(dockManagerName))
                    {
                        // Reset all layouts
                        settings.DockLayoutXml = string.Empty;
                        settings.HasCustomLayout = false;
                        settings.DockLayouts?.Clear();
                    }
                    else
                    {
                        // Reset specific dock manager layout
                        settings.DockLayouts?.Remove(dockManagerName);
                        if (settings.DockLayouts?.Count == 0)
                        {
                            settings.HasCustomLayout = false;
                        }
                    }

                    SaveSettings(settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset layout: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize layout management for a window. Sets up window position tracking.
        /// Layout loading/saving is handled by the attached behavior.
        /// </summary>
        /// <param name="window">The parent window</param>
        public static void InitializeLayoutManagement(Window window)
        {
            _windowSettings = LoadSettings();

            // Restore window position
            RestoreWindowPosition(window);

            // Set up window tracking
            SetupWindowTracking(window);
        }

        /// <summary>
        /// Command implementation for loading dock layout
        /// </summary>
        public static ICommand CreateLoadLayoutCommand()
        {
            return new RelayCommand<DockLayoutParameter>(LoadLayout);
        }

        /// <summary>
        /// Command implementation for saving dock layout
        /// </summary>
        public static ICommand CreateSaveLayoutCommand()
        {
            return new RelayCommand<DockLayoutParameter>(SaveLayout);
        }

        private static void LoadLayout(DockLayoutParameter parameter)
        {
            if (parameter?.DockingManager == null) return;

            try
            {
                var layoutXml = GetDockLayoutXml(parameter.ManagerName);
                if (string.IsNullOrEmpty(layoutXml)) return;

                var layoutSerializer = new XmlLayoutSerializer(parameter.DockingManager);

                // Set up serialization callback to match ContentIds to actual controls
                layoutSerializer.LayoutSerializationCallback += (sender, e) =>
                    OnLayoutSerializationCallback(sender, e, parameter.DockingManager);

                using (var stringReader = new StringReader(layoutXml))
                {
                    layoutSerializer.Deserialize(stringReader);
                }

                System.Diagnostics.Debug.WriteLine($"Successfully loaded layout for {parameter.ManagerName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load layout for {parameter.ManagerName}: {ex.Message}");

                // Clear problematic layout to prevent repeated failures
                if (ex.Message.Contains("Must disconnect specified child") ||
                    ex.Message.Contains("Visual") ||
                    ex.Message.Contains("parent"))
                {
                    System.Diagnostics.Debug.WriteLine($"Clearing problematic layout for {parameter.ManagerName}");
                    SetDockLayoutXml(parameter.ManagerName, string.Empty);
                    SaveSettings(_windowSettings);
                }
            }
        }

        private static void SaveLayout(DockLayoutParameter parameter)
        {
            System.Diagnostics.Debug.WriteLine($"SaveLayout called for {parameter?.ManagerName}");

            if (parameter?.LayoutXml == null) return;

            try
            {
                SetDockLayoutXml(parameter.ManagerName, parameter.LayoutXml);
                _windowSettings.HasCustomLayout = true;
                SaveSettings(_windowSettings);

                System.Diagnostics.Debug.WriteLine($"Successfully saved layout for {parameter.ManagerName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save layout for {parameter.ManagerName}: {ex.Message}");
            }
        }

        private static void OnLayoutSerializationCallback(object sender, LayoutSerializationCallbackEventArgs e, DockingManager dockingManager)
        {
            // For static content defined in XAML, just let AvalonDock handle the positioning
            // Don't try to reassign content that's already in the tree

            System.Diagnostics.Debug.WriteLine($"Layout restore requested for ContentId: {e.Model.ContentId}");

            // Let AvalonDock restore the layout structure without reassigning content
            // The controls are already in place from XAML, we just want their positions restored

            // Don't cancel - let AvalonDock do its thing with the existing content
        }

        private static string GetDockLayoutXml(string dockManagerName)
        {
            if (_windowSettings.DockLayouts == null)
                return string.Empty;

            _windowSettings.DockLayouts.TryGetValue(dockManagerName, out string layoutXml);
            return layoutXml ?? string.Empty;
        }

        private static void SetDockLayoutXml(string dockManagerName, string layoutXml)
        {
            if (_windowSettings.DockLayouts == null)
                _windowSettings.DockLayouts = new Dictionary<string, string>();

            _windowSettings.DockLayouts[dockManagerName] = layoutXml;
        }

        private static void RestoreWindowPosition(Window window)
        {
            try
            {
                window.Left = _windowSettings.Left;
                window.Top = _windowSettings.Top;
                window.Width = _windowSettings.Width;
                window.Height = _windowSettings.Height;
                window.WindowState = _windowSettings.WindowState;

                EnsureWindowIsVisible(window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore window position: {ex.Message}");
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private static void EnsureWindowIsVisible(Window window)
        {
            var windowRect = new System.Drawing.Rectangle(
                (int)window.Left, (int)window.Top,
                (int)window.Width, (int)window.Height);

            bool isVisible = false;
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(windowRect))
                {
                    isVisible = true;
                    break;
                }
            }

            if (!isVisible)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private static void SetupWindowTracking(Window window)
        {
            // Track window changes
            window.LocationChanged += (s, e) => UpdateWindowSettings(window);
            window.SizeChanged += (s, e) => UpdateWindowSettings(window);
            window.StateChanged += (s, e) => UpdateWindowSettings(window);
            window.Closing += (s, e) => OnWindowClosing();
        }

        private static void UpdateWindowSettings(Window window)
        {
            if (window.WindowState == WindowState.Normal)
            {
                _windowSettings.Left = window.Left;
                _windowSettings.Top = window.Top;
                _windowSettings.Width = window.ActualWidth;
                _windowSettings.Height = window.ActualHeight;
            }
            _windowSettings.WindowState = window.WindowState;
        }

        private static void OnWindowClosing()
        {
            // Save final settings
            SaveSettings(_windowSettings);
        }

        /// <summary>
        /// Get all saved dock manager names for debugging
        /// </summary>
        public static List<string> GetSavedDockManagerNames()
        {
            var settings = LoadSettings();
            return settings.DockLayouts?.Keys.ToList() ?? new List<string>();
        }
    }

    /// <summary>
    /// Simple RelayCommand implementation for layout commands
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}