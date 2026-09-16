namespace ColdNet.Core.Security;

/// <summary>No-op protector: values pass through unchanged. Default when no encryption key is wired up (e.g. tests constructing a <c>ModuleExecutionContext</c> directly).</summary>
public sealed class NullSecretProtector : ISecretProtector
{
    public static readonly NullSecretProtector Instance = new();

    public string Protect(string? plaintext) => plaintext ?? string.Empty;

    public string Unprotect(string? value) => value ?? string.Empty;
}
