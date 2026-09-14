using ColdNet.Core.Modules;

namespace ColdNet.EdmVault;

public class EdmVaultExportSettings
{
    /// <summary>Directory EDMVault (or its import watcher) picks finished jobs up from.</summary>
    public string HandoverDirectory { get; set; } = string.Empty;

    /// <summary>Glob (relative to the input directory) selecting which of the job's files to hand over, e.g. "{prefix}.*".</summary>
    public string SourceFileMask { get; set; } = "{prefix}.*";

    public EdmVaultIndexFormat IndexFormat { get; set; } = EdmVaultIndexFormat.Json;

    /// <summary>Moves files into the hand-over directory instead of copying them.</summary>
    public bool MoveFiles { get; set; } = true;
}

/// <summary>
/// The final step of a chain: hands the job's files and collected properties over to EDMVault.
/// In d.cold the same role is played by placing masked output files where d.3 hostimport can
/// pick them up (see <see cref="CommonModuleSettings.MaskForDms"/>); this module makes that
/// explicit as its own pipeline step and adds the index file hostimport would otherwise expect
/// in JPL form.
/// </summary>
[ModuleDefinition("EdmVaultExport", ModuleCategory.DmsExport, "EDMVault Export", "Hands the job's files and properties over to EDMVault via the configured drop directory.", SettingsType = typeof(EdmVaultExportSettings))]
public class EdmVaultExportModule(IEdmVaultHandoverWriter handoverWriter) : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<EdmVaultExportSettings>();

        if (string.IsNullOrWhiteSpace(settings.HandoverDirectory))
        {
            return ModuleExecutionResult.Fail("No EDMVault hand-over directory configured.");
        }

        var mask = settings.SourceFileMask.Replace("{prefix}", context.Job.FilePrefix);
        var files = Directory.EnumerateFiles(context.InputDirectory, mask).ToList();

        if (files.Count == 0)
        {
            return ModuleExecutionResult.Fail($"No files matched '{mask}' in {context.InputDirectory}");
        }

        var properties = await context.LoadPropertiesAsync(cancellationToken);

        var request = new EdmVaultHandoverRequest(
            context.Job.FilePrefix,
            context.InputDirectory,
            files,
            context.DmsSupport.DocumentType,
            properties,
            settings.HandoverDirectory,
            settings.IndexFormat,
            settings.MoveFiles);

        await handoverWriter.WriteAsync(request, cancellationToken);

        return ModuleExecutionResult.Ok();
    }
}
