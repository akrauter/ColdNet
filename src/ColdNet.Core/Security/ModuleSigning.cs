using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ColdNet.Core.Security;

/// <summary>
/// The detached signature stored next to a module assembly as <c>&lt;assembly&gt;.dll.sig</c>.
/// </summary>
public sealed record ModuleSignature(int Version, string Algorithm, string CertificateThumbprintSha256, string Signature);

public sealed record ModuleSignatureVerification(bool IsValid, string? Error)
{
    public static ModuleSignatureVerification Valid { get; } = new(true, null);

    public static ModuleSignatureVerification Invalid(string error) => new(false, error);
}

/// <summary>
/// Detached code signing for module assemblies: the publisher signs each module DLL with the
/// <b>private</b> key of a code-signing certificate (kept in the build/release pipeline, never in the
/// application), and the application only holds the matching <b>public</b> certificate as its trust
/// anchor. Authenticode is deliberately not used - verifying it is Windows-specific, and ColdNet
/// runs in Linux containers too - so this is plain RSA-PSS / ECDSA over SHA-256 via
/// <c>System.Security.Cryptography</c>, identical on every platform.
///
/// What is signed is not just the file's hash: the payload is a fixed, versioned context string plus
/// the file name plus the SHA-256 of the file, so a signature made for one module cannot be
/// re-attached to a different (also legitimately signed) DLL under another name. The trusted
/// certificate is pinned (no chain building): the signature must name exactly that certificate's
/// SHA-256 thumbprint, and the certificate must currently be within its validity period, which forces
/// key rotation. There is no timestamping and no revocation check - rotating the trusted
/// certificate is the revocation mechanism.
/// </summary>
public static class ModuleSigning
{
    public const string SignatureFileExtension = ".sig";
    public const string RsaPssSha256 = "RSA-PSS-SHA256";
    public const string EcdsaSha256 = "ECDSA-SHA256";

    private const int CurrentVersion = 1;
    private const string PayloadContext = "ColdNet-Module-Signature-v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string SignatureFilePathFor(string assemblyPath) => assemblyPath + SignatureFileExtension;

    /// <summary>SHA-256 thumbprint of the certificate (upper-case hex) - the certificate's identity in a signature.</summary>
    public static string ComputeThumbprint(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));

    public static ModuleSignature Sign(string fileName, byte[] fileBytes, X509Certificate2 signingCertificate)
    {
        var payload = BuildPayload(fileName, fileBytes);

        using var rsa = signingCertificate.GetRSAPrivateKey();
        if (rsa is not null)
        {
            var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            return new ModuleSignature(CurrentVersion, RsaPssSha256, ComputeThumbprint(signingCertificate), Convert.ToBase64String(signature));
        }

        using var ecdsa = signingCertificate.GetECDsaPrivateKey();
        if (ecdsa is not null)
        {
            var signature = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
            return new ModuleSignature(CurrentVersion, EcdsaSha256, ComputeThumbprint(signingCertificate), Convert.ToBase64String(signature));
        }

        throw new InvalidOperationException("The signing certificate has no usable RSA or ECDSA private key.");
    }

    public static ModuleSignatureVerification Verify(
        string fileName,
        byte[] fileBytes,
        ModuleSignature signature,
        X509Certificate2 trustedCertificate,
        DateTimeOffset? now = null)
    {
        if (signature.Version != CurrentVersion)
        {
            return ModuleSignatureVerification.Invalid($"unsupported signature version {signature.Version}");
        }

        var trustedThumbprint = ComputeThumbprint(trustedCertificate);
        if (!string.Equals(signature.CertificateThumbprintSha256, trustedThumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return ModuleSignatureVerification.Invalid(
                $"signed by a different certificate (signature names {signature.CertificateThumbprintSha256}, trusted certificate is {trustedThumbprint})");
        }

        var moment = now ?? DateTimeOffset.UtcNow;
        if (moment < new DateTimeOffset(trustedCertificate.NotBefore) || moment > new DateTimeOffset(trustedCertificate.NotAfter))
        {
            return ModuleSignatureVerification.Invalid(
                $"the trusted certificate is not valid at this time (valid {trustedCertificate.NotBefore:u} to {trustedCertificate.NotAfter:u})");
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature.Signature);
        }
        catch (FormatException)
        {
            return ModuleSignatureVerification.Invalid("the signature value is not valid base64");
        }

        var payload = BuildPayload(fileName, fileBytes);

        try
        {
            switch (signature.Algorithm)
            {
                case RsaPssSha256:
                {
                    using var rsa = trustedCertificate.GetRSAPublicKey();
                    if (rsa is null)
                    {
                        return ModuleSignatureVerification.Invalid("the signature is RSA but the trusted certificate has no RSA key");
                    }

                    return rsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                        ? ModuleSignatureVerification.Valid
                        : ModuleSignatureVerification.Invalid("the signature does not match the file (modified after signing, or signed for a different file name)");
                }

                case EcdsaSha256:
                {
                    using var ecdsa = trustedCertificate.GetECDsaPublicKey();
                    if (ecdsa is null)
                    {
                        return ModuleSignatureVerification.Invalid("the signature is ECDSA but the trusted certificate has no ECDSA key");
                    }

                    return ecdsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256)
                        ? ModuleSignatureVerification.Valid
                        : ModuleSignatureVerification.Invalid("the signature does not match the file (modified after signing, or signed for a different file name)");
                }

                default:
                    return ModuleSignatureVerification.Invalid($"unsupported signature algorithm '{signature.Algorithm}'");
            }
        }
        catch (CryptographicException ex)
        {
            return ModuleSignatureVerification.Invalid($"signature verification failed: {ex.Message}");
        }
    }

    public static void WriteSignatureFile(string signaturePath, ModuleSignature signature) =>
        File.WriteAllText(signaturePath, JsonSerializer.Serialize(signature, JsonOptions));

    /// <summary>Reads a signature file, or returns null (with <paramref name="error"/> set) if it is missing or malformed.</summary>
    public static ModuleSignature? ReadSignatureFile(string signaturePath, out string? error)
    {
        if (!File.Exists(signaturePath))
        {
            error = $"no signature file ({Path.GetFileName(signaturePath)})";
            return null;
        }

        try
        {
            var signature = JsonSerializer.Deserialize<ModuleSignature>(File.ReadAllText(signaturePath), JsonOptions);
            if (signature is null || string.IsNullOrWhiteSpace(signature.Signature))
            {
                error = $"signature file {Path.GetFileName(signaturePath)} is empty or incomplete";
                return null;
            }

            error = null;
            return signature;
        }
        catch (JsonException ex)
        {
            error = $"signature file {Path.GetFileName(signaturePath)} is malformed: {ex.Message}";
            return null;
        }
    }

    /// <summary>Loads a public certificate (.cer, DER or PEM) - the application's trust anchor.</summary>
    public static X509Certificate2 LoadPublicCertificate(string path) =>
        X509CertificateLoader.LoadCertificateFromFile(path);

    /// <summary>Loads a signing certificate with its private key from a password-protected PFX.</summary>
    public static X509Certificate2 LoadSigningCertificate(string pfxPath, string password) =>
        X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password, X509KeyStorageFlags.EphemeralKeySet);

    private static byte[] BuildPayload(string fileName, byte[] fileBytes)
    {
        var context = Encoding.UTF8.GetBytes($"{PayloadContext}\0{Path.GetFileName(fileName).ToLowerInvariant()}\0");
        var hash = SHA256.HashData(fileBytes);

        var payload = new byte[context.Length + hash.Length];
        context.CopyTo(payload, 0);
        hash.CopyTo(payload, context.Length);
        return payload;
    }
}
