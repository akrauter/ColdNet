using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ColdNet.Core.Security;
using ColdNet.Engine.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Engine.Tests;

/// <summary>
/// Uses ColdNet.TestPlugin.dll - a real, tiny module assembly this test project references only so
/// it lands in the output folder - as a stand-in for a third-party plugin. No test touches its types
/// directly, so it is never loaded into the default context (which would trip the shadowing check
/// for exactly the reason that check exists).
/// </summary>
public class ModuleTrustTests : IDisposable
{
    private static readonly string TestPluginPath = Path.Combine(AppContext.BaseDirectory, "ColdNet.TestPlugin.dll");

    private readonly string _appDir = Directory.CreateTempSubdirectory("coldnet-trust-app-").FullName;
    private readonly X509Certificate2 _signer = CreateCertificate();
    private readonly X509Certificate2 _trusted;

    public ModuleTrustTests()
    {
        _trusted = X509CertificateLoader.LoadCertificate(_signer.Export(X509ContentType.Cert));
        File.WriteAllBytes(Path.Combine(_appDir, "module-signing.cer"), _trusted.Export(X509ContentType.Cert));
    }

    [Fact]
    public void A_signed_plugin_is_loaded_and_its_module_is_discoverable()
    {
        InstallBuiltIn("Builtin.dll", sign: true);
        InstallPlugin(sign: true);

        var assemblies = ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]);

        var plugin = Assert.Single(assemblies);
        var registry = new ModuleRegistry(assemblies);
        Assert.NotNull(registry.Find("TestPlugin"));
        Assert.Equal("ColdNet.TestPlugin", plugin.GetName().Name);
    }

    [Fact]
    public void An_unsigned_plugin_stops_startup_naming_the_file_and_the_reason()
    {
        InstallBuiltIn("Builtin.dll", sign: true);
        InstallPlugin(sign: false);

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]));

        Assert.Contains("plugins/ColdNet.TestPlugin.dll", ex.Message);
        Assert.Contains("no signature file", ex.Message);
    }

    [Fact]
    public void A_plugin_modified_after_signing_is_rejected()
    {
        InstallBuiltIn("Builtin.dll", sign: true);
        var pluginPath = InstallPlugin(sign: true);

        var bytes = File.ReadAllBytes(pluginPath);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(pluginPath, bytes);

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]));

        Assert.Contains("does not match the file", ex.Message);
    }

    [Fact]
    public void A_plugin_signed_by_an_untrusted_certificate_is_rejected()
    {
        InstallBuiltIn("Builtin.dll", sign: true);
        var pluginPath = InstallPlugin(sign: false);
        using var attacker = CreateCertificate();
        SignFile(pluginPath, attacker);

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]));

        Assert.Contains("different certificate", ex.Message);
    }

    [Fact]
    public void A_tampered_built_in_assembly_stops_startup_even_with_no_plugins()
    {
        var builtInPath = InstallBuiltIn("Builtin.dll", sign: true);
        File.AppendAllText(builtInPath, "malicious payload");

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]));

        Assert.Contains("Builtin.dll", ex.Message);
    }

    [Fact]
    public void Every_problem_is_reported_not_just_the_first()
    {
        InstallBuiltIn("Builtin.dll", sign: false);
        InstallPlugin(sign: false);

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]));

        Assert.Contains("Builtin.dll", ex.Message);
        Assert.Contains("ColdNet.TestPlugin.dll", ex.Message);
    }

    [Fact]
    public void A_missing_trusted_certificate_stops_startup_with_a_pointer_to_the_docs()
    {
        File.Delete(Path.Combine(_appDir, "module-signing.cer"));

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, []));

        Assert.Contains("module-signing.cer", ex.Message);
        Assert.Contains("docs/MODULE-SIGNING.md", ex.Message);
    }

    [Fact]
    public void A_pinned_thumbprint_rejects_a_swapped_certificate_file()
    {
        var options = Options();
        options.TrustedThumbprint = ModuleSigning.ComputeThumbprint(_trusted);
        Assert.Empty(ModuleTrust.VerifyAndLoad(options, NullLogger.Instance, _appDir, []));

        using var swapped = CreateCertificate();
        File.WriteAllBytes(Path.Combine(_appDir, "module-signing.cer"), swapped.Export(X509ContentType.Cert));

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(options, NullLogger.Instance, _appDir, []));
        Assert.Contains("TrustedThumbprint", ex.Message);
    }

    [Fact]
    public void A_plugin_that_would_shadow_a_host_assembly_is_rejected_even_when_signed()
    {
        // ColdNet.Core.dll is loaded in the default context; a plugin-folder copy would get its own
        // ModuleDefinition/IColdModule type identities in a second load context.
        var pluginDir = Directory.CreateDirectory(Path.Combine(_appDir, "plugins")).FullName;
        var shadow = Path.Combine(pluginDir, "ColdNet.Core.dll");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "ColdNet.Core.dll"), shadow);
        SignFile(shadow, _signer);

        var ex = Assert.Throws<ModuleSignatureException>(() =>
            ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, []));

        Assert.Contains("already loaded by the host", ex.Message);
    }

    [Fact]
    public void With_enforcement_off_an_unsigned_plugin_still_loads()
    {
        InstallPlugin(sign: false);
        var options = Options();
        options.Enforce = false;

        var assemblies = ModuleTrust.VerifyAndLoad(options, NullLogger.Instance, _appDir, ["ColdNet.Modules.dll"]);

        Assert.Single(assemblies);
    }

    [Fact]
    public void A_missing_plugin_directory_simply_means_no_plugins()
    {
        InstallBuiltIn("Builtin.dll", sign: true);

        Assert.Empty(ModuleTrust.VerifyAndLoad(Options(), NullLogger.Instance, _appDir, ["Builtin.dll"]));
    }

    private ModuleSigningOptions Options() => new();

    private string InstallBuiltIn(string fileName, bool sign)
    {
        var path = Path.Combine(_appDir, fileName);
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);
        if (sign)
        {
            SignFile(path, _signer);
        }

        return path;
    }

    private string InstallPlugin(bool sign)
    {
        var pluginDir = Directory.CreateDirectory(Path.Combine(_appDir, "plugins")).FullName;
        var path = Path.Combine(pluginDir, "ColdNet.TestPlugin.dll");
        File.Copy(TestPluginPath, path, overwrite: true);
        if (sign)
        {
            SignFile(path, _signer);
        }

        return path;
    }

    private static void SignFile(string path, X509Certificate2 certificate) =>
        ModuleSigning.WriteSignatureFile(
            ModuleSigning.SignatureFilePathFor(path),
            ModuleSigning.Sign(Path.GetFileName(path), File.ReadAllBytes(path), certificate));

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        return new CertificateRequest("CN=ColdNet Test Signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    public void Dispose()
    {
        _signer.Dispose();
        _trusted.Dispose();
        Directory.Delete(_appDir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
