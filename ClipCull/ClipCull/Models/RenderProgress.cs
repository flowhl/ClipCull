using System;

namespace ClipCull.Models
{
    public class RenderProgress
    {
        public double Percentage { get; set; }
        public TimeSpan Elapsed { get; set; }
        public TimeSpan? ETA { get; set; }
        public int? CurrentFrame { get; set; }
        public int? TotalFrames { get; set; }
        public double? Speed { get; set; }
        public double? FPS { get; set; }
        public string RawOutput { get; set; }

        public string DisplayText => FormatDisplay();

        private string FormatDisplay()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (Percentage > 0)
                parts.Add($"{Percentage:F1}%");

            if (CurrentFrame.HasValue && TotalFrames.HasValue)
                parts.Add($"{CurrentFrame}/{TotalFrames}");

            if (Elapsed > TimeSpan.Zero)
                parts.Add($"Elapsed: {Elapsed:hh\\:mm\\:ss}");

            if (ETA.HasValue)
                parts.Add($"ETA: {ETA.Value:hh\\:mm\\:ss}");

            if (Speed.HasValue)
                parts.Add($"{Speed.Value:F1}x");

            if (FPS.HasValue)
                parts.Add($"{FPS.Value:F1} fps");

            return parts.Count > 0 ? string.Join(" | ", parts) : RawOutput ?? string.Empty;
        }
    }
}
