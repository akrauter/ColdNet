using System.Security.Cryptography;
using System.Text;

namespace ColdNet.Core.Security;

/// <summary>
/// AES-256-GCM (authenticated encryption) backed <see cref="ISecretProtector"/>, keyed by a
/// single symmetric key shared by every ColdNet process (Admin, Worker, the emergency
/// <c>ColdNet.SecretTool</c>) - see <see cref="ServiceCollectionExtensions.AddColdNetSecretProtection"/>
/// for where that key comes from. Deliberately not tied to any one machine (no DPAPI/keyring) so
/// it works the same on Windows and Linux/Docker.
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesSecretProtector(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException($"AES-256 key must be 32 bytes, got {key.Length}.", nameof(key));
        }

        _key = key;
    }

    /// <summary>Builds a protector from a base64-encoded 32-byte key, e.g. from configuration.</summary>
    public static AesSecretProtector FromBase64Key(string base64Key) => new(Convert.FromBase64String(base64Key));

    /// <summary>Generates a fresh random 256-bit key, base64-encoded - what "ColdNet.SecretTool generate-key" prints.</summary>
    public static string GenerateBase64Key()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    public string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext ?? string.Empty;
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Not something we encrypted - legacy plaintext, or not a secret field at all. Pass through.
            return value ?? string.Empty;
        }

        try
        {
            var payload = Convert.FromBase64String(value[Prefix.Length..]);
            if (payload.Length < NonceSizeBytes + TagSizeBytes)
            {
                return value;
            }

            var nonce = payload.AsSpan(0, NonceSizeBytes);
            var tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes);
            var ciphertext = payload.AsSpan(NonceSizeBytes + TagSizeBytes);
            var plaintextBytes = new byte[ciphertext.Length];

            using var aesGcm = new AesGcm(_key, TagSizeBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            // Wrong key (e.g. mismatched between processes) or corrupted value - surface as-is
            // rather than silently returning garbage or crashing the whole scheduling pass.
            return value;
        }
    }
}
