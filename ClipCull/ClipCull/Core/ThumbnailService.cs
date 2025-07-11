using FFMpegCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core
{
    public static class ThumbnailService
    {
        public static string GetThumbnail(string videoFilePath)
        {
            string thumbnailPath = System.IO.Path.ChangeExtension(videoFilePath, ".png");
            if (string.IsNullOrEmpty(videoFilePath) || !System.IO.File.Exists(videoFilePath))
            {
                throw new ArgumentException("Invalid video file path provided.");
            }
            // Use FFmpeg to generate a thumbnail from the video
            if (File.Exists(thumbnailPath))
            {
                return thumbnailPath;  // Return existing thumbnail if it already exists
            }
            try
            {
                // Set the FFmpeg binary folder to the external path
                GlobalFFOptions.Current.BinaryFolder = Globals.ExternalPath;

                FFMpeg.Snapshot(
                    videoFilePath,
                    thumbnailPath,
                    new Size(1280, 720),
                    TimeSpan.FromSeconds(1)
                );

                return thumbnailPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate thumbnail.", ex);
            }
        }
    }
}
