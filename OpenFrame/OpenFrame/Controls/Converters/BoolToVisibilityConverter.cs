using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace OpenFrame.Controls.Converters
{
    /// <summary>
    /// Converts boolean values to Visibility values.
    /// True = Visible, False = Collapsed
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to Visibility
        /// </summary>
        /// <param name="value">The boolean value to convert</param>
        /// <param name="targetType">The target type (should be Visibility)</param>
        /// <param name="parameter">Optional parameter to invert the conversion</param>
        /// <param name="culture">The culture info</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;

            // Handle different input types
            if (value is bool boolValue)
            {
                isVisible = boolValue;
            }
            else if (value != null)
            {
                // Try to parse string values
                if (bool.TryParse(value.ToString(), out bool parsedBool))
                {
                    isVisible = parsedBool;
                }
            }

            // Check if we should invert the result
            bool shouldInvert = false;
            if (parameter != null)
            {
                if (parameter is bool invertParam)
                {
                    shouldInvert = invertParam;
                }
                else if (parameter is string stringParam)
                {
                    shouldInvert = string.Equals(stringParam, "invert", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(stringParam, "inverse", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(stringParam, "not", StringComparison.OrdinalIgnoreCase);
                }
            }

            // Apply inversion if requested
            if (shouldInvert)
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility back to boolean
        /// </summary>
        /// <param name="value">The Visibility value to convert</param>
        /// <param name="targetType">The target type (should be bool)</param>
        /// <param name="parameter">Optional parameter to invert the conversion</param>
        /// <param name="culture">The culture info</param>
        /// <returns>True if Visible, False if Collapsed or Hidden</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;

            if (value is Visibility visibility)
            {
                isVisible = visibility == Visibility.Visible;
            }

            // Check if we should invert the result (same logic as Convert)
            bool shouldInvert = false;
            if (parameter != null)
            {
                if (parameter is bool invertParam)
                {
                    shouldInvert = invertParam;
                }
                else if (parameter is string stringParam)
                {
                    shouldInvert = string.Equals(stringParam, "invert", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(stringParam, "inverse", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(stringParam, "not", StringComparison.OrdinalIgnoreCase);
                }
            }

            // Apply inversion if requested
            if (shouldInvert)
            {
                isVisible = !isVisible;
            }

            return isVisible;
        }
    }

    /// <summary>
    /// Inverted version - True = Collapsed, False = Visible
    /// Useful when you want to hide something when a condition is true
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        private static readonly BoolToVisibilityConverter BaseConverter = new BoolToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Use the base converter with invert parameter
            return BaseConverter.Convert(value, targetType, true, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Use the base converter with invert parameter
            return BaseConverter.ConvertBack(value, targetType, true, culture);
        }
    }
}