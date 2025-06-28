using FFMpegCore;
using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Core
{
    /// <summary>
    /// Service for reading video file metadata using FFMpegCore
    /// </summary>
    public static class VideoMetadataReader
    {
        /// <summary>
        /// Reads metadata from a video file asynchronously
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>VideoMetadata object with file information</returns>
        public static async Task<VideoMetadata> ReadMetadataAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return VideoMetadata.CreateError("File path is null or empty");
            }

            if (!File.Exists(filePath))
            {
                return VideoMetadata.CreateError("File does not exist");
            }

            try
            {
                // Get file information
                var fileInfo = new FileInfo(filePath);

                // Analyze video with FFMpegCore
                var mediaInfo = await FFProbe.AnalyseAsync(filePath, new FFOptions { BinaryFolder = Path.GetDirectoryName(Globals.FFProbePath)});

                // Create metadata object
                var metadata = new VideoMetadata
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    Duration = mediaInfo.Duration
                };

                // Extract video stream information
                var primaryVideoStream = mediaInfo.PrimaryVideoStream;
                if (primaryVideoStream != null)
                {
                    metadata.Width = primaryVideoStream.Width;
                    metadata.Height = primaryVideoStream.Height;
                    metadata.FrameRate = primaryVideoStream.FrameRate;
                    metadata.VideoCodec = primaryVideoStream.CodecName?.ToUpperInvariant() ?? "Unknown";
                    metadata.VideoBitrate = primaryVideoStream.BitRate;
                }

                // Extract audio stream information
                var primaryAudioStream = mediaInfo.PrimaryAudioStream;
                if (primaryAudioStream != null)
                {
                    metadata.AudioCodec = primaryAudioStream.CodecName?.ToUpperInvariant() ?? "Unknown";
                    metadata.AudioBitrate = primaryAudioStream.BitRate;
                }

                // Extract recording information from metadata
                ExtractRecordingInformation(mediaInfo, metadata);

                return metadata;
            }
            catch (Exception ex)
            {
                return VideoMetadata.CreateError($"Failed to read metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads metadata from a video file synchronously (blocking)
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>VideoMetadata object with file information</returns>
        public static VideoMetadata ReadMetadata(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return VideoMetadata.CreateError("File path is null or empty");
            }

            if (!File.Exists(filePath))
            {
                return VideoMetadata.CreateError("File does not exist");
            }

            try
            {
                // Get file information
                var fileInfo = new FileInfo(filePath);

                // Analyze video with FFMpegCore (synchronous)
                var mediaInfo = FFProbe.Analyse(filePath, new FFOptions { BinaryFolder = Path.GetDirectoryName(Globals.FFProbePath) });

                // Create metadata object
                var metadata = new VideoMetadata
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime,
                    Duration = mediaInfo.Duration
                };

                // Extract video stream information
                var primaryVideoStream = mediaInfo.PrimaryVideoStream;
                if (primaryVideoStream != null)
                {
                    metadata.Width = primaryVideoStream.Width;
                    metadata.Height = primaryVideoStream.Height;
                    metadata.FrameRate = primaryVideoStream.FrameRate;
                    metadata.VideoCodec = primaryVideoStream.CodecName?.ToUpperInvariant() ?? "Unknown";
                    metadata.VideoBitrate = primaryVideoStream.BitRate;
                }

                // Extract audio stream information
                var primaryAudioStream = mediaInfo.PrimaryAudioStream;
                if (primaryAudioStream != null)
                {
                    metadata.AudioCodec = primaryAudioStream.CodecName?.ToUpperInvariant() ?? "Unknown";
                    metadata.AudioBitrate = primaryAudioStream.BitRate;
                }

                // Extract recording information from metadata
                ExtractRecordingInformation(mediaInfo, metadata);

                return metadata;
            }
            catch (Exception ex)
            {
                return VideoMetadata.CreateError($"Failed to read metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates if a file can be analyzed as a video file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if the file appears to be a valid video file</returns>
        public static bool IsValidVideoFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                // Try to get basic info without full analysis
                var mediaInfo = FFProbe.Analyse(filePath, new FFOptions { BinaryFolder = Path.GetDirectoryName(Globals.FFProbePath) });
                return mediaInfo.PrimaryVideoStream != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets supported video file extensions
        /// </summary>
        /// <returns>Array of supported file extensions</returns>
        public static string[] GetSupportedExtensions()
        {
            return new string[]
            {
                ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv",
                ".webm", ".m4v", ".3gp", ".f4v", ".asf", ".rm",
                ".rmvb", ".vob", ".ogv", ".ts", ".mts", ".m2ts"
            };
        }

        /// <summary>
        /// Creates a file filter string for file dialogs
        /// </summary>
        /// <returns>File filter string for OpenFileDialog</returns>
        public static string GetFileFilter()
        {
            return "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v;*.3gp;*.f4v;*.asf;*.rm;*.rmvb;*.vob;*.ogv;*.ts;*.mts;*.m2ts|" +
                   "MP4 Files|*.mp4|" +
                   "MOV Files|*.mov|" +
                   "AVI Files|*.avi|" +
                   "MKV Files|*.mkv|" +
                   "All Files|*.*";
        }

        #region Private Methods

        /// <summary>
        /// Extracts recording date and camera information from video metadata
        /// </summary>
        private static void ExtractRecordingInformation(IMediaAnalysis mediaInfo, VideoMetadata metadata)
        {
            // Try to get creation time from format metadata
            var creationTime = ExtractCreationTimeFromMetadata(mediaInfo);
            if (creationTime.HasValue)
            {
                metadata.DateRecorded = creationTime.Value;
            }

            // Try to extract camera information
            var cameraModel = ExtractCameraModel(mediaInfo);
            if (!string.IsNullOrEmpty(cameraModel))
            {
                metadata.CameraModel = cameraModel;
            }
        }

        /// <summary>
        /// Extracts creation time from video metadata tags
        /// </summary>
        private static DateTime? ExtractCreationTimeFromMetadata(IMediaAnalysis mediaInfo)
        {
            // Try to get creation time from format metadata
            if (mediaInfo.Format?.Tags != null)
            {
                foreach (var tag in mediaInfo.Format.Tags)
                {
                    if (tag.Key.Equals("creation_time", StringComparison.OrdinalIgnoreCase) ||
                        tag.Key.Equals("date", StringComparison.OrdinalIgnoreCase) ||
                        tag.Key.Equals("encoded_date", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DateTime.TryParse(tag.Value, out var dateTime))
                        {
                            return dateTime;
                        }
                    }
                }
            }

            // Try video stream metadata
            if (mediaInfo.PrimaryVideoStream?.Tags != null)
            {
                foreach (var tag in mediaInfo.PrimaryVideoStream.Tags)
                {
                    if (tag.Key.Equals("creation_time", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DateTime.TryParse(tag.Value, out var dateTime))
                        {
                            return dateTime;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts camera model information from video metadata tags
        /// </summary>
        private static string ExtractCameraModel(IMediaAnalysis mediaInfo)
        {
            // Common metadata keys for camera information
            var cameraKeys = new[] { "device_name", "make", "model", "encoder", "software", "com.apple.quicktime.model" };

            if (mediaInfo.Format?.Tags != null)
            {
                foreach (var tag in mediaInfo.Format.Tags)
                {
                    foreach (var key in cameraKeys)
                    {
                        if (tag.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            var value = tag.Value?.Trim();
                            if (!string.IsNullOrEmpty(value) &&
                                !value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                            {
                                return value;
                            }
                        }
                    }
                }
            }

            // Also check video stream tags
            if (mediaInfo.PrimaryVideoStream?.Tags != null)
            {
                foreach (var tag in mediaInfo.PrimaryVideoStream.Tags)
                {
                    foreach (var key in cameraKeys)
                    {
                        if (tag.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            var value = tag.Value?.Trim();
                            if (!string.IsNullOrEmpty(value) &&
                                !value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                            {
                                return value;
                            }
                        }
                    }
                }
            }

            return null;
        }

        #endregion
    }
}