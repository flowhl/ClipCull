using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenFrame.Models
{
    [Serializable]
    public class WindowSettings
    {
        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double Width { get; set; } = 1400;
        public double Height { get; set; } = 900;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public string DockLayoutXml { get; set; } = string.Empty;
        public bool HasCustomLayout { get; set; } = false; // Track if user has customized layout
    }
}
