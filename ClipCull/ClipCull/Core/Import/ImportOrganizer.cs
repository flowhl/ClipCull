using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClipCull.Models.Import;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// Turns the chosen structure options into concrete destination paths, and resolves free
    /// filenames when the user picks "Rename".
    /// </summary>
    public static class ImportOrganizer
    {
        /// <summary>Format presets offered for the per-day subfolder name.</summary>
        public static readonly string[] DateFormatPresets =
        {
            "yyyy-MM-dd",
            "yyyyMMdd",
            "yyyy-MM-dd dddd",
            "dd.MM.yyyy",
            "yyyy/MM/dd"
        };

        public const string DefaultDateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Subfolder (relative to the target root) an item lands in. Empty string for "same folder".
        /// </summary>
        public static string GetSubfolder(ImportFileItem item, ImportStructureMode mode, ImportDateBasis basis, string dateFormat)
        {
            var date = item.GetDate(basis);
            switch (mode)
            {
                case ImportStructureMode.SameFolder:
                    return string.Empty;
                case ImportStructureMode.SubfolderPerDay:
                    return SanitizeSubpath(date.ToString(string.IsNullOrWhiteSpace(dateFormat) ? DefaultDateFormat : dateFormat, CultureInfo.InvariantCulture));
                case ImportStructureMode.SubfolderPerWeek:
                    return $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):00}";
                case ImportStructureMode.SubfolderPerMonth:
                    return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Sets <see cref="ImportFileItem.DestinationPath"/> to the natural destination (before any rename).
        /// </summary>
        public static void SetNaturalDestination(ImportFileItem item, string targetRoot, ImportStructureMode mode, ImportDateBasis basis, string dateFormat)
        {
            var subfolder = GetSubfolder(item, mode, basis, dateFormat);
            var dir = string.IsNullOrEmpty(subfolder) ? targetRoot : Path.Combine(targetRoot, subfolder);
            item.DestinationPath = Path.Combine(dir, item.FileName);
        }

        /// <summary>
        /// Returns a destination path that collides with neither the filesystem nor
        /// <paramref name="reserved"/>, appending " copy", " copy 2", ... as needed.
        /// </summary>
        public static string GetFreePath(string naturalPath, ISet<string> reserved)
        {
            if (!Collides(naturalPath, reserved))
                return naturalPath;

            var dir = Path.GetDirectoryName(naturalPath);
            var name = Path.GetFileNameWithoutExtension(naturalPath);
            var ext = Path.GetExtension(naturalPath);

            for (int i = 1; ; i++)
            {
                var suffix = i == 1 ? " copy" : $" copy {i}";
                var candidate = Path.Combine(dir!, name + suffix + ext);
                if (!Collides(candidate, reserved))
                    return candidate;
            }
        }

        private static bool Collides(string path, ISet<string> reserved)
        {
            if (File.Exists(path))
                return true;
            return reserved != null && reserved.Contains(Path.GetFullPath(path));
        }

        private static string SanitizeSubpath(string value)
        {
            // Allow directory separators only for the explicit "yyyy/MM/dd" preset; strip other invalids.
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                    continue;
                value = value.Replace(c, '-');
            }
            return value.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
