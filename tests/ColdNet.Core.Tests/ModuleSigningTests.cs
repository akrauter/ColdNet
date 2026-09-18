using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ColdNet.Core.Security;

namespace ColdNet.Core.Tests;

public class ModuleSigningTests
{
    private static readonly byte[] ModuleBytes = "pretend this is ColdNet.Modules.dll"u8.ToArray();

    [Theory]
    [InlineData("rsa")]
    [InlineData("ecdsa")]
    public void A_signature_verifies_against_the_matching_public_certificate(string algorithm)
    {
        using var signer = CreateCertificate(algorithm);
        using var trusted = PublicPart(signer);

        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, signer);
        var result = ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, signature, trusted);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(algorithm == "rsa" ? ModuleSigning.RsaPssSha256 : ModuleSigning.EcdsaSha256, signature.Algorithm);
    }

    [Fact]
    public void A_modified_file_fails_verification()
    {
        using var signer = CreateCertificate("rsa");
        using var trusted = PublicPart(signer);
        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, signer);

        var tampered = ModuleBytes.ToArray();
        tampered[0] ^= 0xFF;

        var result = ModuleSigning.Verify("ColdNet.Modules.dll", tampered, signature, trusted);

        Assert.False(result.IsValid);
        Assert.Contains("does not match the file", result.Error);
    }

    [Fact]
    public void A_signature_cannot_be_reattached_to_a_differently_named_file()
    {
        using var signer = CreateCertificate("rsa");
        using var trusted = PublicPart(signer);
        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, signer);

        // Same bytes, same (valid) signature - but presented as another module's DLL.
        var result = ModuleSigning.Verify("ColdNet.EdmVault.dll", ModuleBytes, signature, trusted);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void The_file_name_comparison_ignores_case_and_directories()
    {
        using var signer = CreateCertificate("rsa");
        using var trusted = PublicPart(signer);
        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, signer);

        var result = ModuleSigning.Verify(@"C:\app\COLDNET.MODULES.DLL", ModuleBytes, signature, trusted);

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void A_signature_from_a_different_certificate_is_rejected_even_if_valid_for_that_certificate()
    {
        using var attacker = CreateCertificate("rsa");
        using var trustedSource = CreateCertificate("rsa");
        using var trusted = PublicPart(trustedSource);
        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, attacker);

        var result = ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, signature, trusted);

        Assert.False(result.IsValid);
        Assert.Contains("different certificate", result.Error);
    }

    [Fact]
    public void A_signature_naming_the_trusted_thumbprint_but_signed_with_another_key_is_rejected()
    {
        using var attacker = CreateCertificate("rsa");
        using var trustedSource = CreateCertificate("rsa");
        using var trusted = PublicPart(trustedSource);

        // Forge the thumbprint field: the claimed identity matches, the cryptography must still fail.
        var forged = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, attacker)
            with { CertificateThumbprintSha256 = ModuleSigning.ComputeThumbprint(trustedSource) };

        var result = ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, forged, trusted);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void An_expired_trusted_certificate_rejects_otherwise_valid_signatures()
    {
        using var signer = CreateCertificate("rsa", notBefore: DateTimeOffset.UtcNow.AddDays(-30), notAfter: DateTimeOffset.UtcNow.AddDays(30));
        using var trusted = PublicPart(signer);
        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, signer);

        var result = ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, signature, trusted, now: DateTimeOffset.UtcNow.AddDays(31));

        Assert.False(result.IsValid);
        Assert.Contains("not valid at this time", result.Error);
    }

    [Fact]
    public void An_unsupported_algorithm_or_version_is_rejected()
    {
        using var signer = CreateCertificate("rsa");
        using var trusted = PublicPart(signer);
        var signature = ModuleSigning.Sign("ColdNet.Modules.dll", ModuleBytes, signer);

        Assert.False(ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, signature with { Algorithm = "MD5-WITH-HOPE" }, trusted).IsValid);
        Assert.False(ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, signature with { Version = 99 }, trusted).IsValid);
        Assert.False(ModuleSigning.Verify("ColdNet.Modules.dll", ModuleBytes, signature with { Signature = "not base64!!" }, trusted).IsValid);
    }

    [Fact]
    public void A_signature_file_round_trips_and_missing_or_malformed_files_report_a_clear_error()
    {
        using var signer = CreateCertificate("ecdsa");
        var dir = Directory.CreateTempSubdirectory("coldnet-sig-test-").FullName;

        try
        {
            var path = Path.Combine(dir, "Mod.dll.sig");
            var signature = ModuleSigning.Sign("Mod.dll", ModuleBytes, signer);

            ModuleSigning.WriteSignatureFile(path, signature);
            Assert.Equal(signature, ModuleSigning.ReadSignatureFile(path, out var error));
            Assert.Null(error);

            Assert.Null(ModuleSigning.ReadSignatureFile(Path.Combine(dir, "missing.sig"), out error));
            Assert.Contains("no signature file", error);

            File.WriteAllText(path, "{ this is not json");
            Assert.Null(ModuleSigning.ReadSignatureFile(path, out error));
            Assert.Contains("malformed", error);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    internal static X509Certificate2 CreateCertificate(string algorithm, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        var from = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        var to = notAfter ?? DateTimeOffset.UtcNow.AddYears(1);

        if (algorithm == "rsa")
        {
            using var rsa = RSA.Create(2048);
            return new CertificateRequest("CN=ColdNet Test Signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                .CreateSelfSigned(from, to);
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new CertificateRequest("CN=ColdNet Test Signing", ecdsa, HashAlgorithmName.SHA256)
            .CreateSelfSigned(from, to);
    }

    /// <summary>What an application actually holds: the certificate without its private key.</summary>
    internal static X509Certificate2 PublicPart(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
}
