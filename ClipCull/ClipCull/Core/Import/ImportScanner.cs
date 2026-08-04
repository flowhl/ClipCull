using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClipCull.Models.Import;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// Recursively scans a source folder / drive for media files of the requested kind.
    /// Only filesystem metadata is read here (fast); duration/thumbnails come later via
    /// <see cref="ImportProbeService"/>.
    /// </summary>
    public static class ImportScanner
    {
        /// <summary>Directory names never descended into (ClipCull helpers + OS junk).</summary>
        private static readonly HashSet<string> SkipDirectories =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".thumbnails", ".trash", "System Volume Information", "$RECYCLE.BIN"
            };

        public static Task<List<ImportFileItem>> ScanAsync(
            string root,
            ImportMediaKind kind,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Scan(root, kind, progress, cancellationToken), cancellationToken);
        }

        private static List<ImportFileItem> Scan(
            string root,
            ImportMediaKind kind,
            IProgress<int> progress,
            CancellationToken cancellationToken)
        {
            var results = new List<ImportFileItem>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return results;

            var extensions = MediaFormats.ForKind(kind);
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dir = stack.Pop();

                // Enumerate subdirectories (skip pruned / inaccessible ones).
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        var name = Path.GetFileName(sub);
                        if (SkipDirectories.Contains(name))
                            continue;
                        stack.Push(sub);
                    }
                }
                catch (UnauthorizedAccessException) { /* skip */ }
                catch (IOException) { /* skip */ }

                // Enumerate files in this directory.
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!extensions.Contains(Path.GetExtension(file)))
                            continue;

                        try
                        {
                            var info = new FileInfo(file);
                            var relativeDir = Path.GetRelativePath(root, info.DirectoryName ?? root);
                            if (relativeDir == ".") relativeDir = string.Empty;

                            results.Add(new ImportFileItem
                            {
                                FullPath = info.FullName,
                                FileName = info.Name,
                                RelativeSubPath = relativeDir,
                                SizeBytes = info.Length,
                                CreatedDate = info.CreationTime,
                                ModifiedDate = info.LastWriteTime,
                                MediaKind = kind
                            });

                            progress?.Report(results.Count);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug($"Skipping unreadable file {file}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException) { /* skip */ }
                catch (IOException) { /* skip */ }
            }

            return results;
        }
    }
}
