using AvalonDock.Layout.Serialization;
using AvalonDock;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace OpenFrame.Behaviors
{
    /// <summary>
    /// Attached behavior to load/save AvalonDock layouts using commands.
    /// This implementation avoids direct DockingManager references in ViewModels.
    /// 
    /// Usage in XAML:
    /// xmlns:behaviors="clr-namespace:OpenFrame.View.Behaviors"
    /// 
    /// <avalonDock:DockingManager 
    ///     behaviors:AvalonDockLayoutSerializer.LoadLayoutCommand="{Binding LoadLayoutCommand}"
    ///     behaviors:AvalonDockLayoutSerializer.SaveLayoutCommand="{Binding SaveLayoutCommand}"
    ///     behaviors:AvalonDockLayoutSerializer.ManagerName="MainDockManager"
    ///     ... />
    /// </summary>
    public static class AvalonDockLayoutSerializer
    {
        #region Dependency Properties

        /// <summary>
        /// LoadLayoutCommand dependency property - executed when DockingManager loads
        /// </summary>
        public static readonly DependencyProperty LoadLayoutCommandProperty =
            DependencyProperty.RegisterAttached("LoadLayoutCommand",
                typeof(ICommand),
                typeof(AvalonDockLayoutSerializer),
                new PropertyMetadata(null, OnLoadLayoutCommandChanged));

        /// <summary>
        /// SaveLayoutCommand dependency property - executed when DockingManager unloads
        /// </summary>
        public static readonly DependencyProperty SaveLayoutCommandProperty =
            DependencyProperty.RegisterAttached("SaveLayoutCommand",
                typeof(ICommand),
                typeof(AvalonDockLayoutSerializer),
                new PropertyMetadata(null, OnSaveLayoutCommandChanged));

        /// <summary>
        /// ManagerName dependency property - identifies this specific DockingManager
        /// </summary>
        public static readonly DependencyProperty ManagerNameProperty =
            DependencyProperty.RegisterAttached("ManagerName",
                typeof(string),
                typeof(AvalonDockLayoutSerializer),
                new PropertyMetadata("DockManager"));

        #endregion

        #region Property Accessors

        public static ICommand GetLoadLayoutCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(LoadLayoutCommandProperty);
        }

        public static void SetLoadLayoutCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(LoadLayoutCommandProperty, value);
        }

        public static ICommand GetSaveLayoutCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(SaveLayoutCommandProperty);
        }

        public static void SetSaveLayoutCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(SaveLayoutCommandProperty, value);
        }

        public static string GetManagerName(DependencyObject obj)
        {
            return (string)obj.GetValue(ManagerNameProperty);
        }

        public static void SetManagerName(DependencyObject obj, string value)
        {
            obj.SetValue(ManagerNameProperty, value);
        }

        #endregion

        #region Event Handlers

        private static void OnLoadLayoutCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement frameworkElement)) return;

            // Remove existing handler to avoid memory leaks
            frameworkElement.Loaded -= OnFrameworkElement_Loaded;

            if (e.NewValue is ICommand)
            {
                // Attach the Load event handler
                frameworkElement.Loaded += OnFrameworkElement_Loaded;
            }
        }

        private static void OnSaveLayoutCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement frameworkElement)) return;

            // Remove existing handler to avoid memory leaks
            frameworkElement.Unloaded -= OnFrameworkElement_Unloaded;

            if (e.NewValue is ICommand)
            {
                // Attach the Unload event handler
                frameworkElement.Unloaded += OnFrameworkElement_Unloaded;
            }
        }

        private static void OnFrameworkElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is DockingManager dockingManager)) return;

            var loadLayoutCommand = GetLoadLayoutCommand(dockingManager);
            if (loadLayoutCommand == null) return;

            var managerName = GetManagerName(dockingManager);

            // Create parameter object with both DockingManager and name
            var parameter = new DockLayoutParameter
            {
                DockingManager = dockingManager,
                ManagerName = managerName
            };

            // Execute command
            if (loadLayoutCommand.CanExecute(parameter))
            {
                loadLayoutCommand.Execute(parameter);
            }
        }

        private static void OnFrameworkElement_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is DockingManager dockingManager)) return;

            var saveLayoutCommand = GetSaveLayoutCommand(dockingManager);
            if (saveLayoutCommand == null) return;

            var managerName = GetManagerName(dockingManager);

            // Serialize the layout to XML
            string xmlLayoutString;
            try
            {
                using (var stringWriter = new StringWriter())
                {
                    var xmlLayout = new XmlLayoutSerializer(dockingManager);
                    xmlLayout.Serialize(stringWriter);
                    xmlLayoutString = stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to serialize layout for {managerName}: {ex.Message}");
                return;
            }

            // Create parameter object with layout XML and name
            var parameter = new DockLayoutParameter
            {
                LayoutXml = xmlLayoutString,
                ManagerName = managerName
            };

            // Execute command
            if (saveLayoutCommand.CanExecute(parameter))
            {
                saveLayoutCommand.Execute(parameter);
            }
        }

        #endregion
    }

    /// <summary>
    /// Parameter class passed to layout commands
    /// </summary>
    public class DockLayoutParameter
    {
        /// <summary>
        /// Name/identifier of the DockingManager
        /// </summary>
        public string ManagerName { get; set; }

        /// <summary>
        /// DockingManager instance (for load operations)
        /// </summary>
        public DockingManager DockingManager { get; set; }

        /// <summary>
        /// Layout XML string (for save operations)
        /// </summary>
        public string LayoutXml { get; set; }
    }
}
