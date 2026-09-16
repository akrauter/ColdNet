namespace ColdNet.Core.Security;

/// <summary>
/// Encrypts/decrypts individual secret values (passwords, passphrases, ...) for storage in
/// <c>ModuleInstance.SettingsJson</c>. Implementations must be safe to call with plain,
/// never-before-protected text (legacy/imported data) - <see cref="Unprotect"/> should return it
/// unchanged rather than throw.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/>. Null/empty input is returned unchanged.</summary>
    string Protect(string? plaintext);

    /// <summary>
    /// Decrypts a value produced by <see cref="Protect"/>. If the input doesn't look like
    /// something this protector produced (e.g. plaintext from before encryption was added, or a
    /// hand-edited value), it is returned unchanged rather than failing.
    /// </summary>
    string Unprotect(string? value);
}
