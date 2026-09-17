using ColdNet.Core.Modules;
using ColdNet.Modules.PropertyExtraction;

namespace ColdNet.Modules.FileHandling;

public class RenameFilesSettings
{
    /// <summary>Source extension (without dot) to rename from, e.g. "$pdf". Ignored when
    /// <see cref="NewFileNamePattern"/> is set.</summary>
    public string FromExtension { get; set; } = string.Empty;

    /// <summary>Target extension (without dot) to rename to, e.g. "pdf". Ignored when
    /// <see cref="NewFileNamePattern"/> is set.</summary>
    public string ToExtension { get; set; } = string.Empty;

    /// <summary>
    /// Optional placeholder-driven new file name (extension included), e.g.
    /// "{JobPrefix}.{Extension}" or "{JobPrefix}_{Counter}.{Extension}". When set, this replaces
    /// FromExtension/ToExtension: every file matching "&lt;prefix&gt;.*" in the input directory is
    /// discovered and renamed with this pattern, instead of requiring one exactly-known source
    /// extension - the reason a plain FromExtension/ToExtension pair can't drop ColdImport's "$"
    /// marker for a chain that handles more than one file type per job.
    /// Placeholders: {JobPrefix}, {DocumentType}, {FileName}, {FileNameWithoutExtension},
    /// {Extension} (with any leading "$" import marker already stripped), {Now:format}, {Counter}.
    /// </summary>
    public string NewFileNamePattern { get; set; } = string.Empty;

    /// <summary>Digit width for {Counter}, e.g. 4 -&gt; "0007" (a larger number is never truncated).</summary>
    public int CounterDigits { get; set; } = 1;

    /// <summary>Padding character used to fill {Counter} up to CounterDigits - only its first character is used.</summary>
    public string CounterFillChar { get; set; } = "0";
}

/// <summary>
/// Renames a job's file(s) - the ColdNet equivalent of CNRENFILES. With a plain
/// <see cref="RenameFilesSettings.FromExtension"/>/<see cref="RenameFilesSettings.ToExtension"/>
/// pair it renames exactly one file with a known extension (e.g. to drop the "$" import marker or
/// hand a file to a module that expects a different extension). Setting
/// <see cref="RenameFilesSettings.NewFileNamePattern"/> switches to a more general mode: every
/// file belonging to the job is discovered on disk and renamed via a placeholder pattern, which
/// also supports an ever-incrementing {Counter} persisted on the module instance (never reset per
/// job).
/// </summary>
[ModuleDefinition("RenameFiles", ModuleCategory.FileHandling, "Rename Files", "Renames a job file's extension, or every job file via a placeholder pattern.", OriginalModule = "CNRENFILES", SettingsType = typeof(RenameFilesSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class RenameFilesModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<RenameFilesSettings>();

        return string.IsNullOrWhiteSpace(settings.NewFileNamePattern)
            ? RenameSingleFile(context, settings)
            : RenameUsingPattern(context, settings, cancellationToken);
    }

    private static Task<ModuleExecutionResult> RenameSingleFile(ModuleExecutionContext context, RenameFilesSettings settings)
    {
        var inputPath = context.GetInputPath(settings.FromExtension);

        if (!File.Exists(inputPath))
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"Input file not found: {inputPath}"));
        }

        var outputPath = context.GetOutputPath(settings.ToExtension);
        File.Move(inputPath, outputPath, overwrite: context.Common.Append);

        return Task.FromResult(ModuleExecutionResult.Ok());
    }

    private static Task<ModuleExecutionResult> RenameUsingPattern(ModuleExecutionContext context, RenameFilesSettings settings, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(context.InputDirectory, context.Job.FilePrefix + ".*")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"No files matched '{context.Job.FilePrefix}.*' in {context.InputDirectory}"));
        }

        Directory.CreateDirectory(context.OutputDirectory);
        var fillChar = string.IsNullOrEmpty(settings.CounterFillChar) ? '0' : settings.CounterFillChar[0];
        var digits = Math.Max(1, settings.CounterDigits);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Placeholders are resolved against a "virtual" file name with any leading "$" import
            // marker already stripped from the extension, so {Extension}/{FileName}/
            // {FileNameWithoutExtension} reflect ColdNet's real, unmarked file identity - not the
            // transient "$"-tagged name CNIMPORT/ColdImport uses on disk to avoid re-importing it.
            var strippedExtension = StripDollarMarker(Path.GetExtension(file));
            var virtualFileName = Path.GetFileNameWithoutExtension(file) + strippedExtension;

            context.ModuleInstance.Counter++;
            var counterText = context.ModuleInstance.Counter
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
                .PadLeft(digits, fillChar);

            var newFileName = SetVariableModule.ResolvePlaceholders(settings.NewFileNamePattern, context, virtualFileName)
                .Replace("{Counter}", counterText);

            var outputPath = Path.Combine(context.OutputDirectory, newFileName);
            File.Move(file, outputPath, overwrite: context.Common.Append);
        }

        return Task.FromResult(ModuleExecutionResult.Ok());
    }

    private static string StripDollarMarker(string extensionWithDot) =>
        extensionWithDot.StartsWith(".$", StringComparison.Ordinal) ? "." + extensionWithDot[2..] : extensionWithDot;
}
