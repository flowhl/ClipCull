using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClipCull.Models.Import;

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

        //Video preview / playback performance
        /// <summary>
        /// When true, the video preview requests GPU (Direct3D11) hardware decoding.
        /// Strongly recommended for high resolution / high framerate footage. When false,
        /// decoding is forced to software, which is much slower for 4K/high-fps clips.
        /// </summary>
        public bool PlaybackHardwareDecoding { get; set; } = true;

        /// <summary>
        /// When true, the video preview trades some decode accuracy for smoother playback of
        /// demanding footage (4K120 etc.): it skips the deblocking loop filter and drops
        /// non-reference frames instead of stuttering. Best paired with hardware decoding, and
        /// especially useful when a rotation is applied (rotation forces CPU work per frame).
        /// </summary>
        public bool PlaybackPerformanceMode { get; set; }

        //Proxy generation (remembers the last choices made in the proxy dialog)
        /// <summary>Target height in pixels for generated proxies. Width preserves aspect ratio.</summary>
        public int ProxyResolutionHeight { get; set; } = 720;
        /// <summary>Target frame rate for generated proxies. 0 = keep the source frame rate.</summary>
        public int ProxyFrameRate { get; set; } = 30;
        /// <summary>Prefer GPU (AMD AMF) encoding for proxies, with a fast software fallback.</summary>
        public bool ProxyUseHardwareEncoding { get; set; } = true;
        /// <summary>Skip files that already have a proxy when generating.</summary>
        public bool ProxySkipExisting { get; set; } = true;

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

        // Import feature
        public string LastImportSourcePath { get; set; }
        public string LastImportTargetPath { get; set; }
        public string ImportDateFormat { get; set; } = "yyyy-MM-dd";
        public ImportStructureMode ImportStructureMode { get; set; } = ImportStructureMode.SubfolderPerDay;
        public ImportDateBasis ImportDateBasis { get; set; } = ImportDateBasis.Created;
        public ImportOperation ImportOperation { get; set; } = ImportOperation.Copy;
        public bool ImportOpenExplorerAfter { get; set; } = true;
        public bool ImportOpenEditorAfter { get; set; }
    }
}
