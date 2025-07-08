using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OpenFrame.Controls.Converters
{
    /// <summary>
    /// Converter that returns true if integer value is greater than the parameter
    /// </summary>
    public class IntegerGreaterThanConverter : IValueConverter
    {
        public static readonly IntegerGreaterThanConverter Instance = new IntegerGreaterThanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out int threshold))
            {
                return intValue > threshold;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
