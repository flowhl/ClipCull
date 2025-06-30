using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace OpenFrame.Models
{
    public class WindowSettings
    {
        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double Width { get; set; } = 1400;
        public double Height { get; set; } = 900;
        public WindowState WindowState { get; set; } = WindowState.Normal;

        // Legacy property for backward compatibility
        public string DockLayoutXml { get; set; } = string.Empty;

        // Dictionary for multiple dock managers - use serializable wrapper
        [XmlIgnore]
        public Dictionary<string, string> DockLayouts { get; set; } = new Dictionary<string, string>();

        // XML Serializable version of DockLayouts
        [XmlArray("DockLayouts")]
        [XmlArrayItem("Layout")]
        public DockLayoutEntry[] DockLayoutsSerializable
        {
            get
            {
                if (DockLayouts == null) return new DockLayoutEntry[0];
                return DockLayouts.Select(kvp => new DockLayoutEntry { Name = kvp.Key, Xml = kvp.Value }).ToArray();
            }
            set
            {
                DockLayouts = new Dictionary<string, string>();
                if (value != null)
                {
                    foreach (var entry in value)
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            DockLayouts[entry.Name] = entry.Xml ?? string.Empty;
                        }
                    }
                }
            }
        }

        public bool HasCustomLayout { get; set; } = false;
    }

    [XmlType("DockLayout")]
    public class DockLayoutEntry
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlText]
        public string Xml { get; set; }
    }
}
