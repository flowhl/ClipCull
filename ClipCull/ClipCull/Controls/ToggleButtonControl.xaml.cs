using System;
using System.Windows;
using System.Windows.Controls;

namespace ClipCull.Controls
{
    /// <summary>
    /// Simple toggle button control that can be bound to a boolean value
    /// </summary>
    public partial class ToggleButtonControl : UserControl
    {
        // Dependency property for the toggle state
        public static readonly DependencyProperty IsToggledProperty =
            DependencyProperty.Register(
                nameof(IsToggled),
                typeof(bool),
                typeof(ToggleButtonControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsToggledChanged));

        // Event for when the toggle state changes
        public event EventHandler<bool> Toggled;

        public ToggleButtonControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets whether the toggle is in the "on" state
        /// </summary>
        public bool IsToggled
        {
            get => (bool)GetValue(IsToggledProperty);
            set => SetValue(IsToggledProperty, value);
        }

        private static void OnIsToggledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToggleButtonControl control)
            {
                control.Toggled?.Invoke(control, (bool)e.NewValue);
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // The binding will handle updating IsToggled, but we can add any additional logic here if needed
        }
    }
}