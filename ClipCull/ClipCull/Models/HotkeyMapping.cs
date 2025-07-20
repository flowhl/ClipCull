using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ClipCull.Models
{
    [Serializable]
    public class HotkeyMapping
    {
        [XmlAttribute]
        public string Hotkey { get; set; }

        [XmlAttribute]
        public string Action { get; set; }

        public HotkeyMapping()
        {
            // Parameterless constructor for XML serialization
        }

        public HotkeyMapping(string hotkey, string action)
        {
            Hotkey = hotkey;
            Action = action;
        }
    }
}
