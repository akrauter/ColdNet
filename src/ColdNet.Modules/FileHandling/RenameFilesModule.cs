using ColdNet.Core.Modules;

namespace ColdNet.Modules.FileHandling;

public class RenameFilesSettings
{
    /// <summary>Source extension (without dot) to rename from.</summary>
    public string FromExtension { get; set; } = string.Empty;

    /// <summary>Target extension (without dot) to rename to.</summary>
    public string ToExtension { get; set; } = string.Empty;
}

/// <summary>
/// Renames a job's file extension - the ColdNet equivalent of DCRENFILES, e.g. used to drop the
/// "$" import marker or to hand a file to a module that expects a different extension.
/// </summary>
[ModuleDefinition("RenameFiles", ModuleCategory.FileHandling, "Rename Files", "Renames a job file's extension.", OriginalModule = "DCRENFILES", SettingsType = typeof(RenameFilesSettings))]
public class RenameFilesModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<RenameFilesSettings>();
        var inputPath = context.GetInputPath(settings.FromExtension);

        if (!File.Exists(inputPath))
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"Input file not found: {inputPath}"));
        }

        var outputPath = context.GetOutputPath(settings.ToExtension);
        File.Move(inputPath, outputPath, overwrite: context.Common.Append);

        return Task.FromResult(ModuleExecutionResult.Ok());
    }
}
