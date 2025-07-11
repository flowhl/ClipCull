using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace ClipCull.Controls.Converters
{
    public class MediaToDrawingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MediaColor mediaColor)
            {
                return DrawingColor.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
            }

            if (value is SolidColorBrush brush)
            {
                var color = brush.Color;
                return DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
            }

            return DrawingColor.White; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DrawingColor drawingColor)
            {
                return MediaColor.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
            }

            return Colors.White; // Fallback
        }
    }
}
