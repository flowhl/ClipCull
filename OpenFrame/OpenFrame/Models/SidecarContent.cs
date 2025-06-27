using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Models
{
    public class SidecarContent
    {
        public List<Marker> Markers { get; set; }
        public List<SubClip> SubClips { get; set; }
        public ClipPoint InPoint { get; set; }
        public ClipPoint OutPoint { get; set; }

        public SidecarContent()
        {
            Markers = new List<Marker>();
            SubClips = new List<SubClip>();
        }
    }
}
