using ColdNet.Core.Modules;

namespace ColdNet.Modules.FileHandling;

public class DeleteFilesSettings
{
    /// <summary>
    /// Extensions (without dot) to delete for this job, e.g. ["tmp", "log"]. Empty = delete every
    /// file matching the job's prefix in the configured directory.
    /// </summary>
    public List<string> Extensions { get; set; } = [];
}

/// <summary>
/// Deletes intermediate files belonging to the job - the ColdNet equivalent of CNDELFILES,
/// typically used at the end of a chain to clean up working files after a successful export.
/// </summary>
[ModuleDefinition("DeleteFiles", ModuleCategory.FileHandling, "Delete Files", "Deletes intermediate files belonging to the job.", OriginalModule = "CNDELFILES", SettingsType = typeof(DeleteFilesSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class DeleteFilesModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<DeleteFilesSettings>();
        var directory = context.InputDirectory;

        if (!Directory.Exists(directory))
        {
            return Task.FromResult(ModuleExecutionResult.Ok());
        }

        foreach (var file in Directory.EnumerateFiles(directory, context.Job.FilePrefix + ".*"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(file).TrimStart('.');
            if (settings.Extensions.Count == 0 || settings.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }

        return Task.FromResult(ModuleExecutionResult.Ok());
    }
}
