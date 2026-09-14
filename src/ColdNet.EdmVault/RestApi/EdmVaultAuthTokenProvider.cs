using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ColdNet.EdmVault.RestApi;

/// <summary>
/// Logs the configured EDMVault service account in once and hands out its JWT, refreshing it
/// shortly before expiry or after a 401. A single instance is shared (singleton) so concurrent
/// job uploads don't each trigger their own login.
/// </summary>
public class EdmVaultAuthTokenProvider(IHttpClientFactory httpClientFactory, IOptions<EdmVaultRestApiOptions> options)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && DateTime.UtcNow < _expiresAtUtc - TimeSpan.FromMinutes(1))
        {
            return _token;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task<string> RefreshAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && DateTime.UtcNow < _expiresAtUtc - TimeSpan.FromMinutes(1))
            {
                return _token;
            }

            var opts = options.Value;
            var client = httpClientFactory.CreateClient(EdmVaultRestApiConstants.HttpClientName);

            var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(opts.UserName, opts.Password), cancellationToken);
            response.EnsureSuccessStatusCode();

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
                        ?? throw new InvalidOperationException("EDMVault login returned an empty response.");

            _token = login.Token;
            _expiresAtUtc = login.ExpiresAtUtc;
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}

internal static class EdmVaultRestApiConstants
{
    public const string HttpClientName = "EdmVaultApi";
}
