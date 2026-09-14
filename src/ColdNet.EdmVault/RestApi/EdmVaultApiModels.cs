namespace ColdNet.EdmVault.RestApi;

// Minimal local copies of the EdmVault.Api DTO shapes actually used by the connector, so
// ColdNet.EdmVault doesn't need a project/assembly reference across repositories - it only needs
// to agree on the JSON contract of EdmVault.Api's "api/auth", "api/projects" and "api/files"
// endpoints (see D:\_Dev\Azubiprojekte\EdmVault\src\EdmVault.Shared\Dtos).

internal sealed record LoginRequest(string UserName, string Password);

internal sealed record LoginResponse(string Token, DateTime ExpiresAtUtc);

internal sealed record EdmVaultProject(Guid Id, string Title);

internal sealed record EdmVaultFileDetail(Guid Id);

internal sealed record SetMetadataRequest(Dictionary<string, string> Metadata);
