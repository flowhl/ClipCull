using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Models
{
    /// <summary>
    /// Represents the type of video clip
    /// </summary>
    public enum ClipType
    {
        /// <summary>
        /// Main clip representing the In/Out point range of the video
        /// </summary>
        MainClip,

        /// <summary>
        /// Sub clip representing a specific segment within the video
        /// </summary>
        SubClip
    }
}
