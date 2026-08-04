namespace ClipCull.Models.Import
{
    /// <summary>
    /// Which kind of media the importer is working with. Controls format filtering,
    /// whether sidecars/metadata are involved, and which post-import actions are offered.
    /// </summary>
    public enum ImportMediaKind
    {
        Video,
        Photo
    }

    /// <summary>
    /// How imported files are laid out under the target folder.
    /// </summary>
    public enum ImportStructureMode
    {
        /// <summary>All files go directly into the target folder.</summary>
        SameFolder,
        /// <summary>One subfolder per capture day.</summary>
        SubfolderPerDay,
        /// <summary>One subfolder per ISO week.</summary>
        SubfolderPerWeek,
        /// <summary>One subfolder per month.</summary>
        SubfolderPerMonth
    }

    /// <summary>
    /// Which file timestamp drives date-based selection and folder structuring.
    /// </summary>
    public enum ImportDateBasis
    {
        Created,
        Modified
    }

    /// <summary>
    /// Whether the import moves the source files or leaves them in place.
    /// </summary>
    public enum ImportOperation
    {
        Copy,
        Move
    }

    /// <summary>
    /// Result of checking a source file against the target tree.
    /// </summary>
    public enum ConflictStatus
    {
        /// <summary>No matching file in the target tree.</summary>
        None,
        /// <summary>Identical content already sits at the exact destination path (likely a re-import).</summary>
        DestinationSameContent,
        /// <summary>A different file already occupies the destination path (name clash).</summary>
        DestinationNameClash,
        /// <summary>Identical content exists elsewhere in the target tree (e.g. moved to a .trash subfolder).</summary>
        ExistsElsewhere
    }

    /// <summary>
    /// What to do with a conflicting file. Which options are valid depends on the <see cref="ConflictStatus"/>.
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>Import normally (no conflict, or user chose to proceed).</summary>
        Import,
        /// <summary>Do not import this file.</summary>
        Skip,
        /// <summary>Replace the file at the destination path.</summary>
        Overwrite,
        /// <summary>Import under a new, non-colliding name.</summary>
        Rename,
        /// <summary>Import even though an identical copy exists somewhere else in the target tree.</summary>
        CopyAnyway
    }
}
