using System.Collections.Generic;

namespace ClipCull.Models
{
    /// <summary>
    /// A workspace groups a set of tags (labels) so the user can keep separate
    /// tag sets for different kinds of footage (e.g. "FPV" and "Concerts").
    /// </summary>
    public class Workspace
    {
        public Workspace()
        {
            Tags = new List<Tag>();
        }

        /// <summary>
        /// Unique, user facing name of the workspace.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tags that belong to this workspace. These are the labels shown in the
        /// tag dropdowns while this workspace is active.
        /// </summary>
        public List<Tag> Tags { get; set; }

        public override string ToString() => Name ?? string.Empty;
    }
}
