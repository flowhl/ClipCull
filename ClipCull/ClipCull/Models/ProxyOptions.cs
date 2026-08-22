using System;

namespace ClipCull.Models
{
    /// <summary>
    /// User-chosen options for a proxy generation batch.
    /// </summary>
    public class ProxyOptions
    {
        /// <summary>Target height in pixels. Width is derived to preserve aspect ratio.</summary>
        public int Height { get; set; } = 720;

        /// <summary>Target frame rate. 0 means keep the source frame rate.</summary>
        public int FrameRate { get; set; } = 30;

        /// <summary>
        /// When true, prefer GPU (AMD AMF) hardware encoding for speed, falling back to a fast
        /// software encode if the hardware encoder is unavailable or fails.
        /// </summary>
        public bool UseHardwareEncoding { get; set; } = true;

        /// <summary>When true, files that already have a proxy are skipped.</summary>
        public bool SkipExisting { get; set; } = true;
    }

    /// <summary>
    /// Progress snapshot for a proxy generation batch, covering both the current file and the
    /// overall batch (used to drive the progress UI and the time estimate).
    /// </summary>
    public class ProxyBatchProgress
    {
        /// <summary>1-based index of the file currently being processed.</summary>
        public int CurrentFileIndex { get; set; }

        /// <summary>Total number of files in the batch.</summary>
        public int TotalFiles { get; set; }

        /// <summary>File name (no directory) of the file currently being processed.</summary>
        public string CurrentFileName { get; set; }

        /// <summary>Completion of the current file, 0-100.</summary>
        public double CurrentFilePercent { get; set; }

        /// <summary>Completion of the whole batch, 0-100 (duration-weighted).</summary>
        public double OverallPercent { get; set; }

        /// <summary>Time elapsed since the batch started.</summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>Estimated time remaining for the whole batch, if it can be computed yet.</summary>
        public TimeSpan? Eta { get; set; }

        /// <summary>Current encode speed multiplier reported by ffmpeg (e.g. 8.0 = 8x realtime).</summary>
        public double? Speed { get; set; }
    }

    /// <summary>
    /// Summary of a completed (or cancelled) proxy generation batch.
    /// </summary>
    public class ProxyBatchResult
    {
        public int Succeeded { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool Cancelled { get; set; }
        public System.Collections.Generic.List<string> Errors { get; } = new System.Collections.Generic.List<string>();
    }
}
