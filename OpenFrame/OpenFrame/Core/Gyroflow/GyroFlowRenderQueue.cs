using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenFrame.Core.Gyroflow.GyroflowSubclipExtractor;

namespace OpenFrame.Core.Gyroflow
{
    public static class GyroFlowRenderQueue
    {
        public static ObservableCollection<SubclipInfo> Subclips = new ObservableCollection<SubclipInfo>();
        
        public static void Enqueue(SubclipInfo info)
        {
            //Check if the subclip already exists in the queue
            if (Subclips.Any(x => x.VideoFile == info.VideoFile && x.StartTime == info.StartTime && x.EndTime == info.EndTime))
            {
                Logger.LogDebug("Subclip already exists in the render queue.", "GyroFlow Render Queue");
                return;
            }
            //Check if the subclip is valid
            if (string.IsNullOrEmpty(info.VideoFile) || !System.IO.File.Exists(info.VideoFile))
            {
                throw new ArgumentException("Invalid video file path provided.");
            }
            Subclips.Add(info);
        }

        public static void Dequeue(SubclipInfo info)
        {
            if (Subclips.Contains(info))
            {
                Subclips.Remove(info);
            }
            else
            {
                Logger.LogWarning("Attempted to remove a subclip that is not in the queue.", "GyroFlow Render Queue");
            }
        }
    }
}
