using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ClipCull.Models.Import;
using FFMpegCore;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// Fills in the "expensive" fields of an <see cref="ImportFileItem"/> in the background:
    /// duration + resolution (video) and a downscaled thumbnail. Thumbnails are cached under
    /// LocalAppData and are NEVER written next to the source file (which may be a read-slow card).
    /// </summary>
    public static class ImportProbeService
    {
        private const int ThumbnailWidth = 240;

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Globals.AppName, "ImportThumbnails");

        static ImportProbeService()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                    Directory.CreateDirectory(CacheDir);
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Probes a single item. Safe to call from a background thread. Never throws.
        /// </summary>
        public static void Probe(ImportFileItem item, CancellationToken cancellationToken = default)
        {
            if (item == null) return;
            item.IsProbing = true;
            try
            {
                if (item.MediaKind == ImportMediaKind.Video)
                    ProbeVideo(item, cancellationToken);
                else
                    ProbePhoto(item, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.LogDebug($"Probe failed for {item.FullPath}: {ex.Message}");
            }
            finally
            {
                item.IsProbing = false;
            }
        }

        private static void ProbeVideo(ImportFileItem item, CancellationToken cancellationToken)
        {
            GlobalFFOptions.Current.BinaryFolder = Globals.ExternalPath;

            // Default 16:9 thumbnail; refined from the probed resolution when available.
            var thumbSize = new System.Drawing.Size(ThumbnailWidth, ThumbnailWidth * 9 / 16);

            try
            {
                var info = FFProbe.Analyse(item.FullPath);
                if (info.Duration != TimeSpan.Zero)
                    item.DurationMs = (long)info.Duration.TotalMilliseconds;

                var v = info.PrimaryVideoStream;
                if (v != null && v.Width > 0 && v.Height > 0)
                {
                    item.Resolution = $"{v.Width}x{v.Height}";
                    int h = (int)Math.Round((double)ThumbnailWidth * v.Height / v.Width);
                    if (h % 2 != 0) h++; // keep even for the encoder
                    thumbSize = new System.Drawing.Size(ThumbnailWidth, Math.Max(2, h));
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"ffprobe failed for {item.FullPath}: {ex.Message}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var thumbPath = GetCachePath(item.FullPath);
            if (File.Exists(thumbPath))
            {
                item.ThumbnailPath = thumbPath;
                return;
            }

            try
            {
                FFMpeg.Snapshot(item.FullPath, thumbPath, thumbSize, TimeSpan.FromSeconds(1));
                if (File.Exists(thumbPath))
                    item.ThumbnailPath = thumbPath;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Snapshot failed for {item.FullPath}: {ex.Message}");
            }
        }

        private static void ProbePhoto(ImportFileItem item, CancellationToken cancellationToken)
        {
            var thumbPath = GetCachePath(item.FullPath);
            if (File.Exists(thumbPath))
            {
                item.ThumbnailPath = thumbPath;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // WPF can decode common raster formats; RAW/HEIC will throw and simply get no thumbnail.
                var decoded = new BitmapImage();
                decoded.BeginInit();
                decoded.CacheOption = BitmapCacheOption.OnLoad;
                decoded.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                decoded.DecodePixelWidth = ThumbnailWidth;
                decoded.UriSource = new Uri(item.FullPath);
                decoded.EndInit();
                decoded.Freeze();

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(decoded));
                using (var fs = new FileStream(thumbPath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }

                item.ThumbnailPath = thumbPath;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Photo thumbnail failed for {item.FullPath}: {ex.Message}");
            }
        }

        private static string GetCachePath(string sourcePath)
        {
            // Key by path + mtime + size so a changed file gets a fresh thumbnail.
            string key = sourcePath;
            try
            {
                var fi = new FileInfo(sourcePath);
                key = $"{sourcePath}|{fi.LastWriteTimeUtc.Ticks}|{fi.Length}";
            }
            catch { /* fall back to path only */ }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var name = Convert.ToHexString(hash);
            return Path.Combine(CacheDir, name + ".png");
        }
    }
}
