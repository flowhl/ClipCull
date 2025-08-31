using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core
{
    public static class SettingsHandler
    {
        public static string SettingsFolder = Globals.SettingsPath;
        public static string SettingsFile = Path.Combine(SettingsFolder, "settings.xml");
        public static SettingsModel Settings { get; set; }

        public static void Initialize()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            if (!File.Exists(SettingsFile))
            {
                Create();
            }
            Load();
        }

        public static void Load()
        {
            Settings = Globals.DeserializeFromFile<SettingsModel>(SettingsFile);
            if (Settings.Tags == null)
            {
                Settings.Tags = new List<Tag>();
            }

            // Ensure SkipSeconds is not 0
            Settings.SkipSeconds = Settings.SkipSeconds <= 0 ? 5 : Settings.SkipSeconds;
        }

        public static void Save()
        {
            // Ensure SkipSeconds is not 0
            Settings.SkipSeconds = Settings.SkipSeconds <= 0 ? 5 : Settings.SkipSeconds;

            Globals.SerializeToFile<SettingsModel>(Settings, SettingsFile);
        }
        private static void Create()
        {
            var newSettings = new SettingsModel();
            Globals.SerializeToFile<SettingsModel>(newSettings, SettingsFile);
        }
    }
}
