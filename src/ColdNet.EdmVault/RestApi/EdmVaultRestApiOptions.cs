namespace ColdNet.EdmVault.RestApi;

/// <summary>Bound from configuration section "ColdNet:EdmVault:RestApi".</summary>
public class EdmVaultRestApiOptions
{
    /// <summary>Base URL of EdmVault.Api, e.g. "https://localhost:7031".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Service account credentials the worker logs in with (POST /api/auth/login).</summary>
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>How long a resolved "document type name -> project id" lookup is cached for.</summary>
    public int ProjectLookupCacheMinutes { get; set; } = 10;
}
