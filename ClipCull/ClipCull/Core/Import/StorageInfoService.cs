using System;
using System.IO;
using ClipCull.Models.Import;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// Reads and writes the <c>info.xml</c> storage-metadata file that can live at the root of a card.
    /// </summary>
    public static class StorageInfoService
    {
        public const string FileName = "info.xml";

        public static string GetPath(string folder) => Path.Combine(folder, FileName);

        public static bool Exists(string folder) =>
            !string.IsNullOrEmpty(folder) && File.Exists(GetPath(folder));

        /// <summary>
        /// Reads storage info from <paramref name="folder"/>\info.xml. Returns null if the file is
        /// absent or not a valid StorageInfo document (some cameras drop unrelated info.xml files).
        /// </summary>
        public static StorageInfo Read(string folder)
        {
            if (!Exists(folder))
                return null;

            try
            {
                var info = Globals.DeserializeFromFile<StorageInfo>(GetPath(folder));
                return info;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Could not read storage info from {GetPath(folder)}: {ex.Message}");
                return null;
            }
        }

        public static void Write(string folder, StorageInfo info)
        {
            if (string.IsNullOrEmpty(folder) || info == null)
                return;

            try
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                Globals.SerializeToFile(info, GetPath(folder));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write storage info to {GetPath(folder)}", ex);
                throw;
            }
        }
    }
}
