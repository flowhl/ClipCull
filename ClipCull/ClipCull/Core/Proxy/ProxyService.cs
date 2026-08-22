using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipCull.Core.Proxy
{
    /// <summary>
    /// Central place for proxy path conventions and lookups.
    ///
    /// A proxy is a lightweight, downscaled copy of a source video used for smooth preview
    /// playback. Proxies live in a hidden ".proxy" folder next to the original and are named
    /// "&lt;original name without extension&gt;.proxy.mp4".
    ///
    /// Example: "D:\Footage\DJI_0001.mp4" -> "D:\Footage\.proxy\DJI_0001.proxy.mp4"
    /// </summary>
    public static class ProxyService
    {
        /// <summary>Name of the hidden folder proxies are stored in, relative to each video.</summary>
        public const string ProxyFolderName = ".proxy";

        /// <summary>Suffix (including extension) used for generated proxy files.</summary>
        public const string ProxySuffix = ".proxy.mp4";

        private static readonly string[] VideoExtensions =
            { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };

        /// <summary>
        /// The ".proxy" folder that holds proxies for videos in the same directory as
        /// <paramref name="videoPath"/>.
        /// </summary>
        public static string GetProxyFolder(string videoPath)
        {
            string dir = Path.GetDirectoryName(videoPath);
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, ProxyFolderName);
        }

        /// <summary>
        /// The full path the proxy for <paramref name="videoPath"/> would have (whether or not it exists).
        /// </summary>
        public static string GetProxyPath(string videoPath)
        {
            string folder = GetProxyFolder(videoPath);
            if (folder == null)
                return null;
            string name = Path.GetFileNameWithoutExtension(videoPath) + ProxySuffix;
            return Path.Combine(folder, name);
        }

        /// <summary>True if a proxy file already exists for the given original video.</summary>
        public static bool HasProxy(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath))
                return false;
            string proxy = GetProxyPath(videoPath);
            return proxy != null && File.Exists(proxy);
        }

        /// <summary>
        /// Returns the proxy path when a proxy exists, otherwise the original path. This is the
        /// path that should actually be handed to the video player for preview/display.
        /// </summary>
        public static string GetPlaybackPath(string videoPath)
        {
            return HasProxy(videoPath) ? GetProxyPath(videoPath) : videoPath;
        }

        /// <summary>True if the given path lives inside a ".proxy" folder or is a generated proxy file.</summary>
        public static bool IsProxyPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.EndsWith(ProxySuffix, StringComparison.OrdinalIgnoreCase))
                return true;

            string dir = Path.GetDirectoryName(path);
            return dir != null &&
                   string.Equals(Path.GetFileName(dir), ProxyFolderName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True if the folder name is the reserved proxy folder.</summary>
        public static bool IsProxyFolder(string folderName)
        {
            return string.Equals(folderName, ProxyFolderName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True if the extension belongs to a supported video file.</summary>
        public static bool IsVideoFile(string path)
        {
            string ext = Path.GetExtension(path);
            return !string.IsNullOrEmpty(ext) &&
                   VideoExtensions.Contains(ext.ToLowerInvariant());
        }

        /// <summary>
        /// Enumerates all source video files in <paramref name="folder"/> (optionally recursing into
        /// subdirectories), skipping any ".proxy" folders and the proxy files themselves.
        /// </summary>
        public static List<string> FindVideoFiles(string folder, bool recursive)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return results;

            CollectVideoFiles(folder, recursive, results);
            return results
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void CollectVideoFiles(string folder, bool recursive, List<string> results)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    if (IsVideoFile(file) && !IsProxyPath(file))
                        results.Add(file);
                }

                if (!recursive)
                    return;

                foreach (var dir in Directory.EnumerateDirectories(folder))
                {
                    if (IsProxyFolder(Path.GetFileName(dir)))
                        continue;
                    CollectVideoFiles(dir, true, results);
                }
            }
            catch (UnauthorizedAccessException) { /* skip inaccessible folders */ }
            catch (IOException) { /* skip transient IO issues */ }
        }
    }
}
