using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClipCull.Core
{
    public static class SettingsHandler
    {
        public const string DefaultWorkspaceName = "Default";

        public static string SettingsFolder = Globals.SettingsPath;
        public static string SettingsFile = Path.Combine(SettingsFolder, "settings.xml");
        public static SettingsModel Settings { get; set; }

        /// <summary>
        /// Raised when the active workspace changes or the set of workspaces is modified,
        /// so tag dropdowns and the workspace selector can refresh.
        /// </summary>
        public static event Action WorkspaceChanged;

        public static void Initialize()
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            if (!File.Exists(SettingsFile))
            {
                Create();
            }
            Load();
        }

        public static void Load()
        {
            Settings = Globals.DeserializeFromFile<SettingsModel>(SettingsFile);
            if (Settings.Tags == null)
            {
                Settings.Tags = new List<Tag>();
            }

            EnsureWorkspaces();

            if (Settings.FolderWorkspaces == null)
                Settings.FolderWorkspaces = new List<FolderWorkspace>();

            // Ensure SkipSeconds is not 0
            Settings.SkipSeconds = Settings.SkipSeconds <= 0 ? 5 : Settings.SkipSeconds;

            // Ensure render settings exist (may be null when loading old settings files)
            if (Settings.DefaultRenderSettings == null)
                Settings.DefaultRenderSettings = new RenderSettings();
        }

        /// <summary>
        /// Guarantees at least one workspace exists and migrates any legacy flat tag list
        /// into the default workspace. Also makes sure a valid current workspace is selected.
        /// </summary>
        private static void EnsureWorkspaces()
        {
            if (Settings.Workspaces == null)
                Settings.Workspaces = new List<Workspace>();

            // Migrate legacy tags (pre-workspace settings files) into a default workspace.
            if (Settings.Workspaces.Count == 0)
            {
                Settings.Workspaces.Add(new Workspace
                {
                    Name = DefaultWorkspaceName,
                    Tags = Settings.Tags != null ? new List<Tag>(Settings.Tags) : new List<Tag>()
                });
            }

            // Legacy list is no longer the source of truth.
            Settings.Tags = new List<Tag>();

            foreach (var workspace in Settings.Workspaces)
            {
                if (workspace.Tags == null)
                    workspace.Tags = new List<Tag>();
            }

            // Ensure the active workspace is valid.
            if (string.IsNullOrEmpty(Settings.CurrentWorkspaceName) ||
                Settings.Workspaces.All(w => !string.Equals(w.Name, Settings.CurrentWorkspaceName, StringComparison.OrdinalIgnoreCase)))
            {
                Settings.CurrentWorkspaceName = Settings.Workspaces[0].Name;
            }
        }

        /// <summary>
        /// The currently active workspace. Never null once settings are loaded.
        /// </summary>
        public static Workspace CurrentWorkspace
        {
            get
            {
                EnsureWorkspaces();
                return Settings.Workspaces.FirstOrDefault(w =>
                           string.Equals(w.Name, Settings.CurrentWorkspaceName, StringComparison.OrdinalIgnoreCase))
                       ?? Settings.Workspaces[0];
            }
        }

        /// <summary>
        /// Tags belonging to the active workspace. These are shown in the tag dropdowns.
        /// </summary>
        public static List<Tag> GetCurrentWorkspaceTags()
        {
            return CurrentWorkspace.Tags ?? new List<Tag>();
        }

        /// <summary>
        /// Switches the active workspace and notifies listeners. Loading existing labels on a
        /// clip always works regardless of the active workspace; only the dropdown is filtered.
        /// </summary>
        public static void SetCurrentWorkspace(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            EnsureWorkspaces();

            var match = Settings.Workspaces.FirstOrDefault(w =>
                string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return;

            if (string.Equals(Settings.CurrentWorkspaceName, match.Name, StringComparison.OrdinalIgnoreCase))
                return;

            Settings.CurrentWorkspaceName = match.Name;
            Save();
            NotifyWorkspaceChanged();
        }

        /// <summary>
        /// Raises <see cref="WorkspaceChanged"/>. Call after the workspace list is edited.
        /// </summary>
        public static void NotifyWorkspaceChanged()
        {
            WorkspaceChanged?.Invoke();
        }

        /// <summary>
        /// Returns the workspace name remembered for the given folder, or null if none.
        /// </summary>
        public static string GetWorkspaceForFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || Settings?.FolderWorkspaces == null)
                return null;

            var normalized = NormalizeFolder(folderPath);
            return Settings.FolderWorkspaces
                .FirstOrDefault(f => string.Equals(NormalizeFolder(f.FolderPath), normalized, StringComparison.OrdinalIgnoreCase))
                ?.WorkspaceName;
        }

        /// <summary>
        /// Stores the workspace that should be active when the given folder is loaded.
        /// </summary>
        public static void RememberWorkspaceForFolder(string folderPath, string workspaceName)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(workspaceName))
                return;

            if (Settings.FolderWorkspaces == null)
                Settings.FolderWorkspaces = new List<FolderWorkspace>();

            var normalized = NormalizeFolder(folderPath);
            var match = Settings.FolderWorkspaces
                .FirstOrDefault(f => string.Equals(NormalizeFolder(f.FolderPath), normalized, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                Settings.FolderWorkspaces.Add(new FolderWorkspace { FolderPath = folderPath, WorkspaceName = workspaceName });
            }
            else
            {
                if (string.Equals(match.WorkspaceName, workspaceName, StringComparison.OrdinalIgnoreCase))
                    return;
                match.WorkspaceName = workspaceName;
            }

            Save();
        }

        private static string NormalizeFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static void Save()
        {
            // Ensure SkipSeconds is not 0
            Settings.SkipSeconds = Settings.SkipSeconds <= 0 ? 5 : Settings.SkipSeconds;

            Globals.SerializeToFile<SettingsModel>(Settings, SettingsFile);
        }
        private static void Create()
        {
            var newSettings = new SettingsModel();
            Globals.SerializeToFile<SettingsModel>(newSettings, SettingsFile);
        }
    }
}
