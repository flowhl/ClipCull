using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Models
{
    public class SettingsModel
    {
        public SettingsModel()
        {
            Tags = new List<Tag>();
            Workspaces = new List<Workspace>();
            FolderWorkspaces = new List<FolderWorkspace>();
        }

        public bool AutosaveSidecar { get; set; }
        public string LastFolderPath { get; set; }
        public bool LoadFileBrowserOnLastFolder { get; set; }

        //Filtering
        public bool FilterMustMatchAllTags { get; set; }

        /// <summary>
        /// When true, the rating filter ignores subclip ratings and only matches against the main clip's UserMetadata rating.
        /// </summary>
        public bool FilterIgnoreSubclipRating { get; set; }

        public int SkipSeconds { get; set; }
        public int VolumeInPercent { get; set; } = 20;

        /// <summary>
        /// Path to the Gyroflow executable.
        /// If null, it will be discovered automatically
        /// </summary>
        public string GyroflowPath { get; set; }
        public string GyroflowSettingsPath { get; set; }
        public bool GyroflowRenderWithRotation { get; set; } = true;
        public bool GyroflowDisableAudio { get; set; }
        public bool GyroflowUseOtherAudioCodec { get; set; }

        // Render engine settings
        public RenderSettings DefaultRenderSettings { get; set; } = new RenderSettings();
        public string AdobeMediaEncoderPath { get; set; }

        // Snapping Settings
        public bool IsMagnetEnabled { get; set; } = true;
        public bool SnapToPlayhead { get; set; } = true;
        public bool SnapToMarkers { get; set; } = true;
        public bool SnapToSubclips { get; set; } = true;
        public bool SnapToInOutPoints { get; set; } = true;
        public double SnapSensitivityPixels { get; set; } = 10.0;

        /// <summary>
        /// Legacy flat tag list. Kept only so old settings files can be migrated into
        /// the default workspace on load. Tags now live inside <see cref="Workspaces"/>.
        /// </summary>
        public List<Tag> Tags { get; set; }

        /// <summary>
        /// All tag workspaces. Each workspace has its own set of labels.
        /// </summary>
        public List<Workspace> Workspaces { get; set; }

        /// <summary>
        /// Name of the currently active workspace whose tags are offered in the dropdowns.
        /// </summary>
        public string CurrentWorkspaceName { get; set; }

        /// <summary>
        /// Remembered workspace per folder loaded in the file browser.
        /// </summary>
        public List<FolderWorkspace> FolderWorkspaces { get; set; }

        public List<HotkeyMapping> HotkeyMappings { get; set; } = new List<HotkeyMapping>();
    }
}
