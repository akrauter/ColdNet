using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Properties;
using ColdNet.Core.Security;
using ColdNet.EdmVault.RestApi;
using Microsoft.Extensions.Logging;

namespace ColdNet.EdmVault;

public class EdmVaultImportSettings
{
    /// <summary>Optional free-text filter passed to EDMVault's file search (GET /api/files?search=).</summary>
    public string SearchFilter { get; set; } = string.Empty;

    public int MaxFilesPerPoll { get; set; } = 20;

    public bool GenerateUniqueJobId { get; set; } = true;

    /// <summary>Writes the EDMVault file's metadata into the job's property bag (&lt;prefix&gt;.properties.json).</summary>
    public bool ImportMetadataAsProperties { get; set; } = true;
}

/// <summary>
/// The reverse of <see cref="EdmVaultExportModule"/>: pulls new files from an EDMVault project via
/// the REST API and creates a job for each, the same way <c>ColdImport</c> watches a local
/// directory. Which project to pull from is the module's DMS support "Document type" field
/// (resolved to a project by title via <see cref="EdmVaultProjectResolver"/>), exactly like the
/// export module uses it as the upload target. EDMVault has no local-file-style "$"-prefix trick
/// to mark a remote item as already handled, so already-imported file ids are tracked in a small
/// local state file (<c>.edmvault-import-state.json</c>) next to the downloaded jobs.
/// </summary>
[ModuleDefinition("EdmVaultImport", ModuleCategory.Import, "EDMVault Import", "Pulls new files from an EDMVault project via the REST API and creates a job for each.", SettingsType = typeof(EdmVaultImportSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class EdmVaultImportModule(
    IHttpClientFactory httpClientFactory,
    EdmVaultAuthTokenProvider tokenProvider,
    EdmVaultProjectResolver projectResolver) : IJobImportModule
{
    private const string StateFileName = ".edmvault-import-state.json";

    public async Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        ISecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        var common = moduleInstance.CommonSettings;
        var settings = string.IsNullOrWhiteSpace(moduleInstance.SettingsJson) || moduleInstance.SettingsJson == "{}"
            ? new EdmVaultImportSettings()
            : JsonSerializer.Deserialize<EdmVaultImportSettings>(moduleInstance.SettingsJson) ?? new EdmVaultImportSettings();

        var results = new List<NewJobRequest>();
        var projectTitle = moduleInstance.DmsSupport.DocumentType;

        if (string.IsNullOrWhiteSpace(projectTitle))
        {
            logger.LogError("EdmVaultImport requires the module's DMS support 'Document type' (used as the EDMVault project title) to be set.");
            return results;
        }

        if (string.IsNullOrWhiteSpace(common.Directory))
        {
            logger.LogError("EdmVaultImport requires a local Directory to download files into.");
            return results;
        }

        Directory.CreateDirectory(common.Directory);

        var client = httpClientFactory.CreateClient(EdmVaultRestApiConstants.HttpClientName);
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Guid projectId;
        try
        {
            projectId = await projectResolver.ResolveAsync(client, projectTitle, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "EdmVaultImport could not resolve project '{ProjectTitle}'", projectTitle);
            return results;
        }

        var query = $"api/files?projectId={projectId}";
        if (!string.IsNullOrWhiteSpace(settings.SearchFilter))
        {
            query += $"&search={Uri.EscapeDataString(settings.SearchFilter)}";
        }

        var files = await client.GetFromJsonAsync<List<EdmVaultFileListItem>>(query, cancellationToken) ?? [];

        var stateFilePath = Path.Combine(common.Directory, StateFileName);
        var seenIds = await LoadSeenIdsAsync(stateFilePath, cancellationToken);
        var newlySeen = false;

        foreach (var file in files.Where(f => !seenIds.Contains(f.Id)).Take(Math.Max(1, settings.MaxFilesPerPoll)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jobNumber = settings.GenerateUniqueJobId
                ? JobNumberGenerator.GenerateUniqueJobId()
                : Path.GetFileNameWithoutExtension(file.Name);
            var localPath = Path.Combine(common.Directory, jobNumber + Path.GetExtension(file.Name));

            try
            {
                await using var responseStream = await client.GetStreamAsync($"api/files/{file.Id}/download", cancellationToken);
                await using var fileStream = File.Create(localPath);
                await responseStream.CopyToAsync(fileStream, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "EdmVaultImport failed to download file {FileId} ({Name})", file.Id, file.Name);
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                continue;
            }

            if (settings.ImportMetadataAsProperties)
            {
                await TryImportMetadataAsync(client, file.Id, common.Directory, jobNumber, logger, cancellationToken);
            }

            seenIds.Add(file.Id);
            newlySeen = true;
            results.Add(new NewJobRequest(jobNumber, common.Directory));
            logger.LogInformation("Imported job {JobNumber} from EDMVault file {FileId} ({Name})", jobNumber, file.Id, file.Name);
        }

        if (newlySeen)
        {
            await SaveSeenIdsAsync(stateFilePath, seenIds, cancellationToken);
        }

        return results;
    }

    private static async Task TryImportMetadataAsync(
        HttpClient client, Guid fileId, string workDirectory, string jobPrefix, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await client.GetFromJsonAsync<EdmVaultFileMetadataResponse>($"api/files/{fileId}", cancellationToken);
            if (detail?.Metadata is null || detail.Metadata.Count == 0)
            {
                return;
            }

            var bag = new PropertyBag();
            foreach (var (key, value) in detail.Metadata)
            {
                bag.Set(key, value);
            }

            var propertiesPath = Path.Combine(workDirectory, jobPrefix + ".properties.json");
            await bag.SaveAsync(propertiesPath, cancellationToken);
        }
        catch (Exception ex)
        {
            // The file itself downloaded fine; missing metadata shouldn't fail the whole import.
            logger.LogWarning(ex, "EdmVaultImport downloaded file {FileId} but could not fetch its metadata", fileId);
        }
    }

    private static async Task<HashSet<Guid>> LoadSeenIdsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        var ids = await JsonSerializer.DeserializeAsync<List<Guid>>(stream, cancellationToken: cancellationToken);
        return ids is null ? [] : [.. ids];
    }

    private static async Task SaveSeenIdsAsync(string path, HashSet<Guid> ids, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, ids, cancellationToken: cancellationToken);
    }
}
