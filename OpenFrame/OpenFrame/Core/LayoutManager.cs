using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Core
{
    public static class LayoutManager
    {
        private static readonly string SettingsPath = Path.Combine(Globals.SettingsPath, "layout.xml");

        public static WindowSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return Globals.DeserializeFromFile<WindowSettings>(SettingsPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load layout: {ex.Message}");
            }

            return new WindowSettings(); // Return defaults
        }

        public static void SaveSettings(WindowSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Globals.SerializeToFile(settings, SettingsPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save layout: {ex.Message}");
            }
        }

        public static void ResetLayout()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var settings = LoadSettings();
                    settings.DockLayoutXml = string.Empty;
                    settings.HasCustomLayout = false;
                    SaveSettings(settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset layout: {ex.Message}");
            }
        }
    }
}
