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
    /// Converter specifically for collection count greater than parameter
    /// </summary>
    public class CollectionCountGreaterThanConverter : IValueConverter
    {
        public static readonly CollectionCountGreaterThanConverter Instance = new CollectionCountGreaterThanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.ICollection collection && parameter is string paramStr && int.TryParse(paramStr, out int threshold))
            {
                return collection.Count > threshold;
            }

            if (value is int count && parameter is string paramStr2 && int.TryParse(paramStr2, out int threshold2))
            {
                return count > threshold2;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
