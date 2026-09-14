namespace ColdNet.Core.Domain;

/// <summary>
/// The settings every d.cold module exposes on its "General" tab (input/output directory,
/// file extensions, Save/backup, delete source, DMS masking, append). Modules that do not need
/// a particular field (e.g. a module with no output file) simply ignore it.
/// </summary>
public class CommonModuleSettings
{
    /// <summary>Directory the module reads its input file(s) from.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>Directory the module writes its output file(s) to. Empty = same as <see cref="Directory"/>.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Extension (without dot) of the file(s) the module consumes, e.g. "pdf".</summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>Extension (without dot) the module writes its output as. Empty = same as <see cref="FileExtension"/>.</summary>
    public string? OutputFileExtension { get; set; }

    /// <summary>
    /// Mirrors d.cold's "Save" checkbox: before processing, copy every file sharing the job's
    /// prefix into a dated sub-folder of SAVE so the original state can always be reprocessed.
    /// </summary>
    public bool Save { get; set; }

    /// <summary>Deletes the source file once it has been processed successfully.</summary>
    public bool DeleteSourceFile { get; set; }

    /// <summary>
    /// Mirrors d.cold's "Mask for d.3" checkbox: escapes/encodes the output so it can be handed
    /// off to the DMS without import errors (here: to EDMVault's file drop-off format).
    /// </summary>
    public bool MaskForDms { get; set; }

    /// <summary>If an output file with the target name already exists, append instead of overwrite.</summary>
    public bool Append { get; set; }
}
