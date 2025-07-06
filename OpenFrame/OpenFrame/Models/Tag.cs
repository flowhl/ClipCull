using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Serialization;

namespace OpenFrame.Models
{
    public class Tag
    {
        /// <summary>
        /// Unique identifier and name for the tag.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Color of the tag in hex format (e.g., "#FF5733").
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Helper property to get Color as System.Windows.Media.Color for binding
        /// </summary>
        [XmlIgnore]
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

        public override bool Equals(object obj)
        {
            return obj is Tag other && Name?.Equals(other.Name, StringComparison.OrdinalIgnoreCase) == true;
        }

        public override int GetHashCode()
        {
            return Name?.ToLowerInvariant().GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return Name ?? string.Empty;
        }
    }
}
