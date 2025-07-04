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
        public UserMetadataContent UserMetadata { get; set; }
        public SidecarContent()
        {
            Markers = new List<Marker>();
            SubClips = new List<SubClip>();
        }
    }

    public class UserMetadataContent
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Location { get; set; }
        /// <summary>
        /// Rating from 1 to 5
        /// </summary>
        public int? Rating { get; set; }
        public List<string> Tags { get; set; }
    }
}
