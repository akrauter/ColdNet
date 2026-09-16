using ColdNet.Core.Modules;

namespace ColdNet.Modules.FileHandling;

public class FileMoverSettings
{
    /// <summary>Also moves job files found in sub-directories of the source directory.</summary>
    public bool IncludeSubdirectories { get; set; }

    public bool OverwriteExisting { get; set; } = true;
}

/// <summary>
/// Moves every file sharing the job's prefix from the input directory to the output directory -
/// the ColdNet equivalent of CNFILEMOVER, typically used between "deliver", "work" and "handover"
/// stages of a chain.
/// </summary>
[ModuleDefinition("FileMover", ModuleCategory.FileHandling, "File Mover", "Moves all of a job's files from the input directory to the output directory.", OriginalModule = "CNFILEMOVER", SettingsType = typeof(FileMoverSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class FileMoverModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<FileMoverSettings>();
        var sourceDir = context.InputDirectory;
        var targetDir = context.OutputDirectory;

        if (!Directory.Exists(sourceDir))
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"Source directory not found: {sourceDir}"));
        }

        Directory.CreateDirectory(targetDir);

        var searchOption = settings.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var moved = 0;

        foreach (var file in Directory.EnumerateFiles(sourceDir, context.Job.FilePrefix + ".*", searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(targetDir, Path.GetFileName(file));
            File.Move(file, target, settings.OverwriteExisting);
            moved++;
        }

        if (moved == 0)
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"No files found for job {context.Job.FilePrefix} in {sourceDir}"));
        }

        return Task.FromResult(ModuleExecutionResult.Ok());
    }
}
