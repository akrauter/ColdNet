using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ColdNet.EdmVault.RestApi;

/// <summary>
/// Delivers a finished job straight to a live EdmVault.Api instance: logs in as the configured
/// service account, resolves <see cref="EdmVaultHandoverRequest.DocumentType"/> to an EDMVault
/// project by title, uploads the job's file(s) (first as the primary document via
/// <c>POST /api/files</c>, any further ones as secondary documents), and writes the job's
/// property bag as the file's metadata via <c>PUT /api/files/{id}/metadata</c>.
/// </summary>
public class RestApiEdmVaultHandoverWriter(
    IHttpClientFactory httpClientFactory,
    EdmVaultAuthTokenProvider tokenProvider,
    EdmVaultProjectResolver projectResolver) : IEdmVaultHandoverWriter
{
    public async Task WriteAsync(EdmVaultHandoverRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await WriteCoreAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Token might have been invalidated server-side; refresh once and retry.
            await tokenProvider.RefreshAsync(cancellationToken);
            await WriteCoreAsync(request, cancellationToken);
        }
    }

    private async Task WriteCoreAsync(EdmVaultHandoverRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            throw new InvalidOperationException(
                "The EDMVault REST connector requires the module's DMS support 'Document type' to be set - it is used as the EDMVault project title.");
        }

        var client = await CreateAuthorizedClientAsync(ct);
        var projectId = await projectResolver.ResolveAsync(client, request.DocumentType, ct);

        if (request.SourceFiles.Count == 0)
        {
            return;
        }

        var fileId = await UploadPrimaryAsync(client, projectId, request.SourceFiles[0], ct);

        for (var i = 1; i < request.SourceFiles.Count; i++)
        {
            await UploadSecondaryAsync(client, fileId, request.SourceFiles[i], ct);
        }

        var metadata = request.Properties.Values.ToDictionary(kv => kv.Key, kv => kv.Value.FirstOrDefault() ?? string.Empty);
        await SetMetadataAsync(client, fileId, metadata, ct);

        if (request.MoveFiles)
        {
            foreach (var file in request.SourceFiles)
            {
                File.Delete(file);
            }
        }
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(EdmVaultRestApiConstants.HttpClientName);
        var token = await tokenProvider.GetTokenAsync(ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> UploadPrimaryAsync(HttpClient client, Guid projectId, string filePath, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(projectId.ToString()), "projectId");

        using var stream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(stream);
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await client.PostAsync("api/files", form, ct);
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<EdmVaultFileDetail>(cancellationToken: ct)
                     ?? throw new InvalidOperationException("EDMVault upload returned an empty response.");
        return detail.Id;
    }

    private static async Task UploadSecondaryAsync(HttpClient client, Guid fileId, string filePath, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        using var stream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(stream);
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await client.PostAsync($"api/files/{fileId}/secondary", form, ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SetMetadataAsync(HttpClient client, Guid fileId, Dictionary<string, string> metadata, CancellationToken ct)
    {
        var response = await client.PutAsJsonAsync($"api/files/{fileId}/metadata", new SetMetadataRequest(metadata), ct);
        response.EnsureSuccessStatusCode();
    }
}
