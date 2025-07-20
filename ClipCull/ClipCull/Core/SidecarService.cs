using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core
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
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save sidecar content", ex);
            }
        }

        #region Equality Methods
        public static bool Equals(SidecarContent a, SidecarContent b)
        {
            // Handle null cases
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            // Compare ClipPoints (assuming they have proper Equals implementation)
            if (!object.Equals(a.InPoint, b.InPoint)) return false;
            if (!object.Equals(a.OutPoint, b.OutPoint)) return false;

            // Compare UserMetadata
            if (!EqualsUserMetadata(a.UserMetadata, b.UserMetadata)) return false;

            // Compare Markers collection (order-independent)
            if (!EqualsMarkers(a.Markers, b.Markers)) return false;

            // Compare SubClips collection (order-independent)
            if (!EqualsSubClips(a.SubClips, b.SubClips)) return false;

            return true;
        }

        private static bool EqualsUserMetadata(UserMetadataContent a, UserMetadataContent b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            if(a.Tags == null) a.Tags = new ObservableCollection<Tag>();
            if (b.Tags == null) b.Tags = new ObservableCollection<Tag>();

            return a.Title == b.Title &&
                   a.Description == b.Description &&
                   a.Author == b.Author &&
                   a.Location == b.Location &&
                   a.Reel == b.Reel &&
                   a.Shot == b.Shot &&
                   a.Camera == b.Camera &&
                   a.Rating == b.Rating &&
                   a.Pick == b.Pick &&
                   EqualsTags(a.Tags.ToList(), b.Tags.ToList());
        }

        private static bool EqualsTags(List<Tag> a, List<Tag> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            // Order-independent comparison using HashSet
            var setA = new HashSet<Tag>(a ?? Enumerable.Empty<Tag>());
            var setB = new HashSet<Tag>(b ?? Enumerable.Empty<Tag>());
            return setA.SetEquals(setB);
        }

        private static bool EqualsMarkers(List<Marker> a, List<Marker> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            // Order-independent comparison
            // Since Marker has proper Equals implementation, we can use HashSet
            var setA = new HashSet<Marker>(a);
            var setB = new HashSet<Marker>(b);

            return setA.SetEquals(setB);
        }

        private static bool EqualsSubClips(List<SubClip> a, List<SubClip> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            // Order-independent comparison
            // Since SubClip has proper Equals implementation (based on Id), we can use HashSet
            var setA = new HashSet<SubClip>(a);
            var setB = new HashSet<SubClip>(b);

            return setA.SetEquals(setB);
        }
        #endregion
    }
}
