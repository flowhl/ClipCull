using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClipCull.Models.Import;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// Single source of truth for which file extensions the importer treats as video or photo.
    /// Video reuses <see cref="VideoMetadataReader.GetSupportedExtensions"/>.
    /// </summary>
    public static class MediaFormats
    {
        public static readonly HashSet<string> VideoExtensions =
            new(VideoMetadataReader.GetSupportedExtensions(), StringComparer.OrdinalIgnoreCase);

        public static readonly HashSet<string> PhotoExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Common raster
                ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".bmp", ".heic", ".heif", ".gif",
                // RAW formats
                ".dng", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2", ".raf", ".orf",
                ".rw2", ".pef", ".srw", ".raw", ".gpr", ".3fr", ".fff", ".iiq", ".x3f", ".erf",
                ".mef", ".mos", ".mrw", ".dcr", ".kdc", ".cap"
            };

        public static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));
        public static bool IsPhoto(string path) => PhotoExtensions.Contains(Path.GetExtension(path));

        public static HashSet<string> ForKind(ImportMediaKind kind) =>
            kind == ImportMediaKind.Video ? VideoExtensions : PhotoExtensions;

        public static bool Matches(string path, ImportMediaKind kind) =>
            ForKind(kind).Contains(Path.GetExtension(path));
    }
}
