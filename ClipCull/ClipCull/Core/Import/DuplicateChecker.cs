using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ClipCull.Models.Import;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// File hashing helpers. <see cref="Hash"/> goes through the global persistent
    /// <see cref="HashCache"/>; <see cref="Compute"/> is the raw streaming SHA-256 used on a miss.
    /// </summary>
    public static class FileHasher
    {
        /// <summary>Cache-backed hash (global persistent cache + in-memory). Preferred entry point.</summary>
        public static string Hash(string path) => HashCache.GetHash(path);

        /// <summary>Raw streaming SHA-256, no caching. Reads the whole file.</summary>
        public static string Compute(string path)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
    }

    /// <summary>
    /// An index of every file already present in the target tree (including subfolders such as
    /// <c>.trash</c>), used to detect whether a source file has already been imported.
    /// </summary>
    public class TargetIndex
    {
        private class Entry
        {
            public string FullPath;
            public long Size;
        }

        // filename (case-insensitive) -> matching files in the target tree
        private readonly Dictionary<string, List<Entry>> _byName = new(StringComparer.OrdinalIgnoreCase);

        public string TargetRoot { get; private set; }

        public static Task<TargetIndex> BuildAsync(string targetRoot, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Build(targetRoot, cancellationToken), cancellationToken);
        }

        private static TargetIndex Build(string targetRoot, CancellationToken cancellationToken)
        {
            var index = new TargetIndex { TargetRoot = targetRoot };
            if (string.IsNullOrEmpty(targetRoot) || !Directory.Exists(targetRoot))
                return index;

            var stack = new Stack<string>();
            stack.Push(targetRoot);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dir = stack.Pop();

                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        // Keep .trash (that's the point), but ignore thumbnail caches.
                        if (string.Equals(Path.GetFileName(sub), ".thumbnails", StringComparison.OrdinalIgnoreCase))
                            continue;
                        stack.Push(sub);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            var name = fi.Name;
                            if (!index._byName.TryGetValue(name, out var list))
                            {
                                list = new List<Entry>();
                                index._byName[name] = list;
                            }
                            list.Add(new Entry { FullPath = fi.FullName, Size = fi.Length });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug($"Target index skip {file}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            return index;
        }

        /// <summary>
        /// Classifies a source item against the target tree and fills its conflict fields.
        /// <see cref="ImportFileItem.DestinationPath"/> must already be set by the organizer.
        /// Convenience wrapper – must be called on the UI thread (it mutates bound properties).
        /// </summary>
        public void Classify(ImportFileItem item)
        {
            var (status, existingPath) = Evaluate(item);
            ApplyResult(item, status, existingPath);
        }

        /// <summary>
        /// Computes the conflict status for an item WITHOUT touching any bound properties, so it is
        /// safe to run on a background thread. Apply the result on the UI thread via <see cref="ApplyResult"/>.
        /// <see cref="ImportFileItem.DestinationPath"/> must already be set by the organizer.
        /// </summary>
        public (ConflictStatus status, string existingPath) Evaluate(ImportFileItem item)
        {
            var result = ConflictStatus.None;
            string existingPath = null;

            try
            {
                // 1) Destination-path collision takes priority.
                if (!string.IsNullOrEmpty(item.DestinationPath) && File.Exists(item.DestinationPath))
                {
                    existingPath = item.DestinationPath;
                    result = SameContent(item.FullPath, item.DestinationPath)
                        ? ConflictStatus.DestinationSameContent
                        : ConflictStatus.DestinationNameClash;
                }
                else if (_byName.TryGetValue(item.FileName, out var candidates))
                {
                    // 2) Same content somewhere else in the tree (e.g. moved to .trash).
                    var destFull = string.IsNullOrEmpty(item.DestinationPath)
                        ? null
                        : Path.GetFullPath(item.DestinationPath);

                    var sourceFull = Path.GetFullPath(item.FullPath);

                    foreach (var c in candidates.Where(c => c.Size == item.SizeBytes))
                    {
                        var candidateFull = Path.GetFullPath(c.FullPath);

                        // A file is never a duplicate of itself – important for "reorganize in
                        // place", where the source file lives inside the target tree being indexed.
                        if (string.Equals(candidateFull, sourceFull, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (destFull != null &&
                            string.Equals(candidateFull, destFull, StringComparison.OrdinalIgnoreCase))
                            continue; // that's the destination, handled above

                        if (SameContentByHash(item.FullPath, c.FullPath))
                        {
                            result = ConflictStatus.ExistsElsewhere;
                            existingPath = c.FullPath;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Conflict check failed for {item.FullPath}: {ex.Message}");
            }

            return (result, existingPath);
        }

        /// <summary>
        /// Applies a computed conflict result to the item. Mutates bound properties, so it MUST run
        /// on the UI thread.
        /// </summary>
        public static void ApplyResult(ImportFileItem item, ConflictStatus status, string existingPath)
        {
            item.ConflictStatus = status;
            item.ConflictExistingPath = existingPath;
            ApplyDefaultResolution(item);
        }

        private static bool SameContent(string a, string b)
        {
            try
            {
                if (new FileInfo(a).Length != new FileInfo(b).Length)
                    return false;
                return SameContentByHash(a, b);
            }
            catch { return false; }
        }

        private static bool SameContentByHash(string a, string b)
        {
            // Both sides go through the global persistent hash cache.
            return string.Equals(FileHasher.Hash(a), FileHasher.Hash(b), StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyDefaultResolution(ImportFileItem item)
        {
            switch (item.ConflictStatus)
            {
                case ConflictStatus.DestinationSameContent:
                    item.AvailableResolutions = new() { ConflictResolution.Skip, ConflictResolution.Overwrite, ConflictResolution.Rename };
                    item.ConflictResolutionChoice = ConflictResolution.Skip;
                    break;
                case ConflictStatus.DestinationNameClash:
                    item.AvailableResolutions = new() { ConflictResolution.Rename, ConflictResolution.Overwrite, ConflictResolution.Skip };
                    item.ConflictResolutionChoice = ConflictResolution.Rename;
                    break;
                case ConflictStatus.ExistsElsewhere:
                    item.AvailableResolutions = new() { ConflictResolution.Skip, ConflictResolution.CopyAnyway, ConflictResolution.Rename };
                    item.ConflictResolutionChoice = ConflictResolution.Skip;
                    break;
                default:
                    item.AvailableResolutions = new() { ConflictResolution.Import };
                    item.ConflictResolutionChoice = ConflictResolution.Import;
                    break;
            }
        }
    }
}
