using OpenFrame.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Core
{
    public static class SidecarService
    {
        public static SidecarContent GetSidecarContent(string videoFile)
        {
            string sidecarPath = Path.ChangeExtension(videoFile, ".xml");
            if (!File.Exists(sidecarPath))
            {
                return new SidecarContent();
            }

            var sidecarContent = Globals.DeserializeFromFile<SidecarContent>(sidecarPath);
            if (sidecarContent != null)
            {
                if (sidecarContent.UserMetadata == null)
                    sidecarContent.UserMetadata = new UserMetadataContent();
                return sidecarContent;
            }

            Logger.LogError($"Failed to load sidecar content from {Path.GetFileName(sidecarPath)}");
            throw new InvalidDataException("Invalid sidecar content format.");
        }

        public static void SaveSidecarContent(SidecarContent sidecar, string videoFile)
        {
            string sidecarPath = Path.ChangeExtension(videoFile, ".xml");
            if (sidecar == null)
            {
                Logger.LogInfo("No sidecar content to save.");
                return;
            }
            try
            {
                Globals.SerializeToFile(sidecar, sidecarPath);
                Logger.LogInfo($"Sidecar content saved to {sidecarPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save sidecar content", ex);
            }
        }
    }
}
