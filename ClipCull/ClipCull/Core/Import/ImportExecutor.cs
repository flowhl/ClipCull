using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClipCull.Models;
using ClipCull.Models.Import;

namespace ClipCull.Core.Import
{
    /// <summary>Progress snapshot raised during an import run.</summary>
    public class ImportProgress
    {
        public int Processed { get; set; }
        public int Total { get; set; }
        public string CurrentFile { get; set; }
    }

    /// <summary>Summary of a finished import run.</summary>
    public class ImportResult
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Performs the actual move/copy of selected files, applying per-file conflict resolution and
    /// (for video) seeding sidecars with the shared metadata.
    /// </summary>
    public static class ImportExecutor
    {
        public static Task<ImportResult> RunAsync(
            IReadOnlyList<ImportFileItem> items,
            ImportOperation operation,
            UserMetadataContent metadataTemplate,
            IProgress<ImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Run(items, operation, metadataTemplate, progress, cancellationToken), cancellationToken);
        }

        private static ImportResult Run(
            IReadOnlyList<ImportFileItem> items,
            ImportOperation operation,
            UserMetadataContent metadataTemplate,
            IProgress<ImportProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new ImportResult();
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = items.Count;
            int processed = 0;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                progress?.Report(new ImportProgress { Processed = processed, Total = total, CurrentFile = item.FileName });

                var resolution = item.ConflictResolutionChoice;
                if (resolution == ConflictResolution.Skip)
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    bool overwrite = resolution == ConflictResolution.Overwrite;
                    string finalPath = item.DestinationPath;

                    if (resolution == ConflictResolution.Rename)
                    {
                        finalPath = ImportOrganizer.GetFreePath(finalPath, reserved);
                    }
                    else if (!overwrite && Collides(finalPath, reserved))
                    {
                        // The destination is already occupied even though it wasn't flagged as a
                        // conflict up front (e.g. an earlier partial import, or two source files
                        // landing in the same day-folder with the same name during this run).
                        if (IsSameFile(item.FullPath, finalPath))
                        {
                            // Identical file already present – treat as already imported.
                            result.Skipped++;
                            continue;
                        }

                        // Different file with the same name – keep both rather than fail.
                        finalPath = ImportOrganizer.GetFreePath(finalPath, reserved);
                    }

                    if (string.Equals(Path.GetFullPath(item.FullPath), Path.GetFullPath(finalPath), StringComparison.OrdinalIgnoreCase))
                    {
                        // Source already is the destination – nothing to do.
                        result.Skipped++;
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

                    if (operation == ImportOperation.Move)
                        File.Move(item.FullPath, finalPath, overwrite);
                    else
                        File.Copy(item.FullPath, finalPath, overwrite);

                    reserved.Add(Path.GetFullPath(finalPath));

                    // The ClipCull .xml sidecar for video is handled specially (metadata merge);
                    // exclude it from the generic companion sweep so it isn't handled twice.
                    if (item.MediaKind == ImportMediaKind.Video)
                    {
                        WriteSidecar(item.FullPath, finalPath, operation, metadataTemplate);
                        MoveCompanions(item.FullPath, finalPath, operation, overwrite, ClipCullSidecarOnly);
                    }
                    else
                    {
                        MoveCompanions(item.FullPath, finalPath, operation, overwrite, null);
                    }

                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"{item.FileName}: {ex.Message}");
                    Logger.LogErrorToFile($"Import failed for '{item.FullPath}' -> '{item.DestinationPath}': {ex}");
                }
            }

            return result;
        }

        /// <summary>
        /// Sidecar / companion extensions that should travel with their media file. Covers ClipCull
        /// (.xml), Lightroom / Camera Raw / Bridge (.xmp), camera thumbnails/proxies (.thm, .lrv),
        /// Apple edits (.aae) and a few common RAW editors (.dop, .pp3, .on1, .reti).
        /// </summary>
        private static readonly string[] SidecarExtensions =
            { ".xml", ".xmp", ".thm", ".lrv", ".aae", ".dop", ".pp3", ".on1", ".reti" };

        /// <summary>Exclude the ClipCull .xml from the generic sweep (handled by WriteSidecar).</summary>
        private static readonly ISet<string> ClipCullSidecarOnly =
            new HashSet<string>(new[] { ".xml" }, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Moves/copies any sidecar files that belong to <paramref name="sourceMedia"/> alongside the
        /// media into its new home, renaming them to match if the media itself was renamed. Handles
        /// both the "NAME.ext" (replace-extension, e.g. Lightroom) and "NAME.EXT.ext" (appended) styles.
        /// </summary>
        private static void MoveCompanions(string sourceMedia, string finalMedia, ImportOperation op, bool overwrite, ISet<string> excludeExt)
        {
            try
            {
                var srcDir = Path.GetDirectoryName(sourceMedia)!;
                var srcName = Path.GetFileNameWithoutExtension(sourceMedia); // NAME
                var srcFile = Path.GetFileName(sourceMedia);                 // NAME.EXT
                var destDir = Path.GetDirectoryName(finalMedia)!;
                var destName = Path.GetFileNameWithoutExtension(finalMedia);
                var destFile = Path.GetFileName(finalMedia);

                foreach (var ext in SidecarExtensions)
                {
                    if (excludeExt != null && excludeExt.Contains(ext))
                        continue;

                    // Replace-extension style: NAME.ext  (e.g. IMG_0001.xmp)
                    TryMoveCompanion(Path.Combine(srcDir, srcName + ext), Path.Combine(destDir, destName + ext), op, overwrite);

                    // Appended style: NAME.EXT.ext  (e.g. IMG_0001.CR3.xmp)
                    TryMoveCompanion(Path.Combine(srcDir, srcFile + ext), Path.Combine(destDir, destFile + ext), op, overwrite);
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorToFile($"Companion sweep failed for '{sourceMedia}': {ex}");
            }
        }

        private static void TryMoveCompanion(string src, string dest, ImportOperation op, bool overwrite)
        {
            if (!File.Exists(src)) return;
            if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (op == ImportOperation.Move)
                    File.Move(src, dest, overwrite);
                else
                    File.Copy(src, dest, overwrite);
            }
            catch (Exception ex)
            {
                Logger.LogErrorToFile($"Companion sidecar move failed '{src}' -> '{dest}': {ex}");
            }
        }

        private static bool Collides(string path, ISet<string> reserved)
        {
            return File.Exists(path) || reserved.Contains(Path.GetFullPath(path));
        }

        private static bool IsSameFile(string a, string b)
        {
            try
            {
                if (new FileInfo(a).Length != new FileInfo(b).Length)
                    return false;
                return string.Equals(FileHasher.Hash(a), FileHasher.Hash(b), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Seeds/updates the sidecar next to the imported video. An existing source sidecar is
        /// carried over and only its empty fields are filled from the template; otherwise a fresh
        /// sidecar is created prefilled from the template. Rating is intentionally never set.
        /// </summary>
        private static void WriteSidecar(string sourceMedia, string finalMedia, ImportOperation operation, UserMetadataContent template)
        {
            try
            {
                var sourceSidecar = Path.ChangeExtension(sourceMedia, ".xml");
                var destSidecar = Path.ChangeExtension(finalMedia, ".xml");

                SidecarContent sidecar;
                if (File.Exists(sourceSidecar))
                {
                    // Carry the existing sidecar across, then fill only the gaps.
                    sidecar = SidecarService.GetSidecarContent(sourceMedia);
                    MergeInto(sidecar.UserMetadata, template);

                    // Move/copy leaves the original sidecar path; on move, remove the stale one.
                    if (operation == ImportOperation.Move)
                    {
                        try { if (File.Exists(sourceSidecar)) File.Delete(sourceSidecar); } catch { }
                    }
                }
                else
                {
                    sidecar = new SidecarContent();
                    sidecar.UserMetadata = CloneTemplate(template);
                }

                SidecarService.SaveSidecarContent(sidecar, finalMedia);
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Sidecar seeding failed for {finalMedia}: {ex.Message}");
            }
        }

        private static UserMetadataContent CloneTemplate(UserMetadataContent t)
        {
            var m = new UserMetadataContent
            {
                Title = t?.Title,
                Description = t?.Description,
                Author = t?.Author,
                Location = t?.Location,
                Reel = t?.Reel,
                Shot = t?.Shot,
                Camera = t?.Camera
                // Rating / Pick intentionally left unset.
            };
            if (t?.Tags != null && t.Tags.Count > 0)
                m.Tags = new ObservableCollection<Tag>(t.Tags);
            return m;
        }

        private static void MergeInto(UserMetadataContent existing, UserMetadataContent t)
        {
            if (existing == null || t == null) return;
            if (string.IsNullOrWhiteSpace(existing.Title)) existing.Title = t.Title;
            if (string.IsNullOrWhiteSpace(existing.Description)) existing.Description = t.Description;
            if (string.IsNullOrWhiteSpace(existing.Author)) existing.Author = t.Author;
            if (string.IsNullOrWhiteSpace(existing.Location)) existing.Location = t.Location;
            if (string.IsNullOrWhiteSpace(existing.Reel)) existing.Reel = t.Reel;
            if (string.IsNullOrWhiteSpace(existing.Shot)) existing.Shot = t.Shot;
            if (string.IsNullOrWhiteSpace(existing.Camera)) existing.Camera = t.Camera;
            if ((existing.Tags == null || existing.Tags.Count == 0) && t.Tags != null && t.Tags.Count > 0)
                existing.Tags = new ObservableCollection<Tag>(t.Tags);
        }
    }
}
