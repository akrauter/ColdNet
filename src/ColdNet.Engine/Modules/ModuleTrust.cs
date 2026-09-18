using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

namespace ColdNet.Engine.Modules;

/// <summary>Bound from the <c>ColdNet:ModuleSigning</c> configuration section.</summary>
public sealed class ModuleSigningOptions
{
    /// <summary>
    /// When true (the default), every module assembly - built-in and plugin - must carry a valid
    /// detached signature (<c>&lt;name&gt;.dll.sig</c>) from the trusted certificate, or the host
    /// refuses to start. Only turn this off for local development; it logs a loud warning each start.
    /// </summary>
    public bool Enforce { get; set; } = true;

    /// <summary>The trusted <b>public</b> certificate (.cer) module signatures are verified against. Relative paths resolve against the application directory.</summary>
    public string TrustedCertificatePath { get; set; } = "module-signing.cer";

    /// <summary>
    /// Optional extra pin: the SHA-256 thumbprint (hex) the trusted certificate file must have. Makes
    /// swapping the certificate file alone - without also changing configuration - not enough.
    /// </summary>
    public string TrustedThumbprint { get; set; } = string.Empty;

    /// <summary>Directory scanned for external module assemblies (top level only). Relative paths resolve against the application directory; a missing directory just means "no plugins".</summary>
    public string PluginDirectory { get; set; } = "plugins";
}

public sealed class ModuleSignatureException(string message) : Exception(message);

/// <summary>
/// Startup gate that decides which module assemblies the host may use: verifies the built-in module
/// assemblies on disk (before they are loaded, so a tampered DLL's module initializer never runs),
/// and loads external plugin assemblies from the plugin directory - only ever from the very bytes
/// that were verified, never re-read from disk afterwards.
/// </summary>
public static class ModuleTrust
{
    /// <summary>The built-in module assemblies every host references at compile time.</summary>
    public static readonly string[] BuiltInAssemblyFileNames = ["ColdNet.Modules.dll", "ColdNet.EdmVault.dll"];

    /// <summary>
    /// Verifies the built-in assemblies and loads any plugin assemblies, returning the plugin assemblies
    /// (the caller adds its own built-in ones). Throws <see cref="ModuleSignatureException"/> - listing
    /// every problem found, not just the first - if enforcement is on and anything fails.
    /// </summary>
    public static IReadOnlyList<Assembly> VerifyAndLoad(
        ModuleSigningOptions options,
        ILogger logger,
        string baseDirectory,
        IEnumerable<string> builtInAssemblyFileNames)
    {
        X509Certificate2? trusted = null;

        if (options.Enforce)
        {
            trusted = LoadTrustedCertificate(options, baseDirectory);
            WarnIfExpiringSoon(trusted, logger);
        }
        else
        {
            logger.LogWarning(
                "Module signature verification is DISABLED (ColdNet:ModuleSigning:Enforce = false). Any module assembly, " +
                "signed or not, will be trusted - never run like this outside local development.");
        }

        var problems = new List<string>();

        if (trusted is not null)
        {
            foreach (var fileName in builtInAssemblyFileNames)
            {
                var path = Path.Combine(baseDirectory, fileName);
                if (!File.Exists(path))
                {
                    problems.Add($"{fileName}: built-in module assembly not found in {baseDirectory}");
                    continue;
                }

                var result = VerifyFile(path, File.ReadAllBytes(path), trusted);
                if (!result.IsValid)
                {
                    problems.Add($"{fileName}: {result.Error}");
                }
            }
        }

        var plugins = LoadPluginAssemblies(options, logger, baseDirectory, trusted, problems);

        if (problems.Count > 0)
        {
            throw new ModuleSignatureException(
                "Refusing to start: module signature verification failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => "  - " + p)) + Environment.NewLine +
                "See docs/MODULE-SIGNING.md (sign the modules with ColdNet.SignTool, or - local development only - set ColdNet:ModuleSigning:Enforce to false).");
        }

        return plugins;
    }

    /// <summary>Verifies <paramref name="bytes"/> against the detached signature file next to <paramref name="assemblyPath"/>.</summary>
    public static ModuleSignatureVerification VerifyFile(string assemblyPath, byte[] bytes, X509Certificate2 trustedCertificate)
    {
        var signature = ModuleSigning.ReadSignatureFile(ModuleSigning.SignatureFilePathFor(assemblyPath), out var readError);
        return signature is null
            ? ModuleSignatureVerification.Invalid(readError!)
            : ModuleSigning.Verify(Path.GetFileName(assemblyPath), bytes, signature, trustedCertificate);
    }

    private static X509Certificate2 LoadTrustedCertificate(ModuleSigningOptions options, string baseDirectory)
    {
        var path = Path.GetFullPath(options.TrustedCertificatePath, baseDirectory);
        if (!File.Exists(path))
        {
            throw new ModuleSignatureException(
                $"Refusing to start: the trusted module-signing certificate '{path}' does not exist. " +
                "Ship the public certificate (.cer) next to the application, point ColdNet:ModuleSigning:TrustedCertificatePath at it, " +
                "or - local development only - set ColdNet:ModuleSigning:Enforce to false. See docs/MODULE-SIGNING.md.");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = ModuleSigning.LoadPublicCertificate(path);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new ModuleSignatureException($"Refusing to start: '{path}' is not a readable certificate: {ex.Message}");
        }

        if (!string.IsNullOrWhiteSpace(options.TrustedThumbprint))
        {
            var actual = ModuleSigning.ComputeThumbprint(certificate);
            var expected = options.TrustedThumbprint.Replace(" ", string.Empty);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new ModuleSignatureException(
                    $"Refusing to start: '{path}' has thumbprint {actual}, but ColdNet:ModuleSigning:TrustedThumbprint pins {expected}.");
            }
        }

        return certificate;
    }

    private static void WarnIfExpiringSoon(X509Certificate2 certificate, ILogger logger)
    {
        var remaining = new DateTimeOffset(certificate.NotAfter) - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.FromDays(30))
        {
            logger.LogWarning(
                "The trusted module-signing certificate expires on {NotAfter:u} - after that, every module signature fails verification and the host refuses to start. Rotate it (see docs/MODULE-SIGNING.md).",
                certificate.NotAfter);
        }
    }

    private static IReadOnlyList<Assembly> LoadPluginAssemblies(
        ModuleSigningOptions options,
        ILogger logger,
        string baseDirectory,
        X509Certificate2? trusted,
        List<string> problems)
    {
        var directory = Path.GetFullPath(options.PluginDirectory, baseDirectory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var verified = new List<(string FileName, string AssemblyName, byte[] Bytes)>();

        foreach (var path in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(path);
            var bytes = File.ReadAllBytes(path);

            if (trusted is not null)
            {
                var result = VerifyFile(path, bytes, trusted);
                if (!result.IsValid)
                {
                    problems.Add($"plugins/{fileName}: {result.Error}");
                    continue;
                }
            }
            else
            {
                logger.LogWarning("Loading plugin assembly {FileName} WITHOUT signature verification.", fileName);
            }

            string assemblyName;
            try
            {
                using var pe = new PEReader(new MemoryStream(bytes));
                assemblyName = pe.GetMetadataReader().GetAssemblyDefinition().GetAssemblyName().Name
                    ?? throw new BadImageFormatException("assembly has no name");
            }
            catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException)
            {
                problems.Add($"plugins/{fileName}: not a managed assembly ({ex.Message})");
                continue;
            }

            // A plugin must not shadow an assembly the host already runs (e.g. ColdNet.Core.dll or a
            // framework assembly copied into the plugin folder): a second copy in another load context
            // would give the plugin's ModuleDefinition/IColdModule types a different identity than the
            // host's, so its modules would silently never be recognised - or, worse, replace host code.
            if (AssemblyLoadContext.Default.Assemblies.Any(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add($"plugins/{fileName}: assembly '{assemblyName}' is already loaded by the host - remove it from the plugin directory (plugins may only add new assemblies)");
                continue;
            }

            verified.Add((fileName, assemblyName, bytes));
        }

        if (problems.Count > 0 || verified.Count == 0)
        {
            return [];
        }

        var context = new PluginLoadContext();
        var loaded = new List<Assembly>();
        foreach (var (fileName, assemblyName, bytes) in verified)
        {
            loaded.Add(context.LoadVerified(assemblyName, bytes));
            logger.LogInformation("Loaded module plugin assembly {FileName}", fileName);
        }

        return loaded;
    }

    /// <summary>
    /// Loads plugin assemblies from already-verified bytes, and lets plugin assemblies reference each
    /// other (everything else - ColdNet.Core, the framework - resolves in the default context).
    /// </summary>
    private sealed class PluginLoadContext() : AssemblyLoadContext("ColdNet.ModulePlugins", isCollectible: false)
    {
        private readonly Dictionary<string, Assembly> _loaded = new(StringComparer.OrdinalIgnoreCase);

        public Assembly LoadVerified(string assemblyName, byte[] bytes)
        {
            var assembly = LoadFromStream(new MemoryStream(bytes));
            _loaded[assemblyName] = assembly;
            return assembly;
        }

        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name is not null && _loaded.TryGetValue(assemblyName.Name, out var assembly) ? assembly : null;
    }
}
