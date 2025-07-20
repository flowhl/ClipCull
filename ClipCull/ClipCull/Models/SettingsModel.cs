using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Models
{
    public class SettingsModel
    {
        public SettingsModel()
        {
            Tags = new List<Tag>();
        }

        public bool AutosaveSidecar { get; set; }
        public string LastFolderPath { get; set; }

        //Filtering
        public bool FilterMustMatchAllTags { get; set; }


        /// <summary>
        /// Path to the Gyroflow executable.
        /// If null, it will be discovered automatically
        /// </summary>
        public string GyroflowPath { get; set; }
        public string GyroflowSettingsPath { get; set; }

        public List<Tag> Tags { get; set; }
        public List<HotkeyMapping> HotkeyMappings { get; set; } = new List<HotkeyMapping>();
    }
}
