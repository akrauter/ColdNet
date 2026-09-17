using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

// TODO: move this file into ColdNet.Modules (e.g. next to ColdImportModule.cs) or your own plugin
// assembly, and adjust the namespace below to match.
namespace ColdNet.Modules.Import;

public class ImportModuleTemplateSettings
{
    /// <summary>Generates a unique job number instead of deriving one from the source item's own
    /// name/id - see JobNumberGenerator.GenerateUniqueJobId(). Recommended default; only turn this
    /// off if downstream steps genuinely need the job's FilePrefix to match the source name.</summary>
    public bool GenerateUniqueJobId { get; set; } = true;
}

/// <summary>
/// TODO: describe what this import module watches/pulls from, and when you'd use it instead of
/// ColdImport (local directory) or SftpImport (remote SFTP/FTPS/FTP).
/// </summary>
[ModuleDefinition("ImportModuleTemplate", ModuleCategory.Import, "MODULE_DISPLAY_NAME", "MODULE_DESCRIPTION", SettingsType = typeof(ImportModuleTemplateSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class ImportModuleTemplateModule : IJobImportModule
{
    public Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        ISecretProtector secretProtector,
        ISet<string> existingFilePrefixes,
        CancellationToken cancellationToken)
    {
        // An import module has no ModuleExecutionContext (it runs before any Job exists), so its
        // settings are read directly off the raw ModuleInstance - same pattern as ColdImportModule.
        var settings = string.IsNullOrWhiteSpace(moduleInstance.SettingsJson) || moduleInstance.SettingsJson == "{}"
            ? new ImportModuleTemplateSettings()
            : System.Text.Json.JsonSerializer.Deserialize<ImportModuleTemplateSettings>(moduleInstance.SettingsJson) ?? new ImportModuleTemplateSettings();

        var results = new List<NewJobRequest>();

        // TODO: discover new work here (e.g. scan a directory, call a remote API) and, for each
        // item found, resolve a unique job id and add a NewJobRequest:
        //
        //   var candidateId = settings.GenerateUniqueJobId
        //       ? JobNumberGenerator.GenerateUniqueJobId()
        //       : <derive an id from the source item's own name>;
        //   var jobNumber = JobNumberGenerator.MakeUnique(candidateId, existingFilePrefixes);
        //   results.Add(new NewJobRequest(jobNumber, workDirectory));
        //
        // MakeUnique guarantees the returned id doesn't collide with any job this chain already
        // has (including ones kept from long-finished runs) - resolve it BEFORE renaming/moving
        // any file to match, so the physical file and the reported FilePrefix always agree.
        //
        // See ColdNet.Modules/Import/ColdImportModule.cs (local directory, the "$"-rename
        // dedup trick) or ColdNet.Modules/RemoteTransfer/SftpImportModule.cs (remote polling with
        // a local de-dup state file) for complete worked examples.

        return Task.FromResult<IReadOnlyList<NewJobRequest>>(results);
    }
}
