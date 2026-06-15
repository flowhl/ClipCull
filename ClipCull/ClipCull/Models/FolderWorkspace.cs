namespace ClipCull.Models
{
    /// <summary>
    /// Remembers which tag workspace was active for a given folder in the file browser,
    /// so reloading that folder restores the matching workspace.
    /// </summary>
    public class FolderWorkspace
    {
        public string FolderPath { get; set; }
        public string WorkspaceName { get; set; }
    }
}
