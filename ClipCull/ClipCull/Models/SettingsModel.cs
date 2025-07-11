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

        /// <summary>
        /// Path to the Gyroflow executable.
        /// If null, it will be discovered automatically
        /// </summary>
        public string GyroflowPath { get; set; }
        public string GyroflowSettingsPath { get; set; }

        public List<Tag> Tags { get; set; }
    }
}
