using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ColdNet.EdmVault.RestApi;

/// <summary>
/// Resolves a "document type" (used throughout ColdNet as the human-readable field on every
/// module's DMS support tab) to an EDMVault project id via <c>GET /api/projects?search=</c>,
/// caching the result briefly. Shared by <see cref="RestApiEdmVaultHandoverWriter"/> (export) and
/// <see cref="EdmVaultImportModule"/> (import) so both sides resolve the same way.
/// </summary>
public class EdmVaultProjectResolver(IOptions<EdmVaultRestApiOptions> options)
{
    private static readonly ConcurrentDictionary<string, (Guid ProjectId, DateTime CachedAtUtc)> Cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<Guid> ResolveAsync(HttpClient client, string documentTypeTitle, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(documentTypeTitle, out var cached) &&
            DateTime.UtcNow - cached.CachedAtUtc < TimeSpan.FromMinutes(options.Value.ProjectLookupCacheMinutes))
        {
            return cached.ProjectId;
        }

        var projects = await client.GetFromJsonAsync<List<EdmVaultProject>>(
            $"api/projects?search={Uri.EscapeDataString(documentTypeTitle)}", cancellationToken) ?? [];
        var match = projects.FirstOrDefault(p => string.Equals(p.Title, documentTypeTitle, StringComparison.OrdinalIgnoreCase))
                    ?? projects.FirstOrDefault();

        if (match is null)
        {
            throw new InvalidOperationException($"No EDMVault project found matching document type '{documentTypeTitle}'.");
        }

        Cache[documentTypeTitle] = (match.Id, DateTime.UtcNow);
        return match.Id;
    }
}
