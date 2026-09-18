using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ColdNet.Core.Security;

// ColdNet.SignTool - the publisher-side counterpart of the host's module signature check.
// The PFX password is only ever read from the COLDNET_SIGNING_PFX_PASSWORD environment variable,
// never from a command-line argument (arguments end up in shell history and process listings).

const string PasswordVariable = "COLDNET_SIGNING_PFX_PASSWORD";

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

try
{
    return args[0] switch
    {
        "generate-cert" => GenerateCertificate(ParseOptions(args[1..], out _)),
        "export-cer" => ExportCer(ParseOptions(args[1..], out _)),
        "sign" => Sign(ParseOptions(args[1..], out var signFiles), signFiles),
        "verify" => Verify(ParseOptions(args[1..], out var verifyFiles), verifyFiles),
        _ => Fail($"Unknown command '{args[0]}'."),
    };
}
catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
{
    return Fail(ex.Message);
}

static void PrintUsage()
{
    Console.WriteLine("""
        ColdNet.SignTool - sign and verify ColdNet module assemblies

          generate-cert --subject "CN=ColdNet Module Signing" --pfx signing.pfx --cer signing.cer [--years 5] [--algorithm rsa|ecdsa]
              Creates a self-signed code-signing certificate. signing.pfx (private key) stays with the
              build/release pipeline; signing.cer (public) is what applications trust.
          export-cer --pfx signing.pfx --out module-signing.cer
              Extracts the public certificate from a PFX.
          sign --pfx signing.pfx <assembly.dll>...
              Writes <assembly.dll>.sig next to each file.
          verify --cer module-signing.cer <assembly.dll>...
              Checks each file against its .sig using the public certificate.

        The PFX password is read from the COLDNET_SIGNING_PFX_PASSWORD environment variable.
        """);
}

static int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    return 1;
}

static Dictionary<string, string> ParseOptions(string[] args, out List<string> positional)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    positional = [];

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Option {args[i]} needs a value.");
            }

            options[args[i][2..]] = args[++i];
        }
        else
        {
            positional.Add(args[i]);
        }
    }

    return options;
}

static string Required(Dictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required option --{name}.");

static string RequirePassword()
{
    var password = Environment.GetEnvironmentVariable(PasswordVariable);
    return string.IsNullOrEmpty(password)
        ? throw new ArgumentException($"Set the {PasswordVariable} environment variable to the PFX password.")
        : password;
}

static void WritePrivateFile(string path, byte[] content)
{
    File.WriteAllBytes(path, content);
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

static int GenerateCertificate(Dictionary<string, string> options)
{
    var subject = Required(options, "subject");
    var pfxPath = Required(options, "pfx");
    var cerPath = Required(options, "cer");
    var years = options.TryGetValue("years", out var y) ? int.Parse(y, System.Globalization.CultureInfo.InvariantCulture) : 5;
    var algorithm = options.GetValueOrDefault("algorithm", "rsa");
    var password = RequirePassword();

    CertificateRequest request;
    RSA? rsa = null;
    ECDsa? ecdsa = null;

    switch (algorithm.ToLowerInvariant())
    {
        case "rsa":
            rsa = RSA.Create(3072);
            request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            break;
        case "ecdsa":
            ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA384);
            break;
        default:
            throw new ArgumentException("--algorithm must be rsa or ecdsa.");
    }

    using (rsa)
    using (ecdsa)
    {
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3", "Code Signing")], false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(years));

        WritePrivateFile(pfxPath, certificate.Export(X509ContentType.Pfx, password));
        File.WriteAllBytes(cerPath, certificate.Export(X509ContentType.Cert));

        Console.WriteLine($"Created {pfxPath} (PRIVATE KEY - keep it out of the repository and the application) and {cerPath} (public).");
        Console.WriteLine($"Valid until {certificate.NotAfter:u}. SHA-256 thumbprint: {ModuleSigning.ComputeThumbprint(certificate)}");
    }

    return 0;
}

static int ExportCer(Dictionary<string, string> options)
{
    using var certificate = ModuleSigning.LoadSigningCertificate(Required(options, "pfx"), RequirePassword());
    var outPath = Required(options, "out");
    File.WriteAllBytes(outPath, certificate.Export(X509ContentType.Cert));
    Console.WriteLine($"Wrote {outPath} (SHA-256 thumbprint {ModuleSigning.ComputeThumbprint(certificate)}).");
    return 0;
}

static int Sign(Dictionary<string, string> options, List<string> files)
{
    if (files.Count == 0)
    {
        throw new ArgumentException("Give at least one assembly to sign.");
    }

    using var certificate = ModuleSigning.LoadSigningCertificate(Required(options, "pfx"), RequirePassword());

    foreach (var file in files)
    {
        var signature = ModuleSigning.Sign(Path.GetFileName(file), File.ReadAllBytes(file), certificate);
        ModuleSigning.WriteSignatureFile(ModuleSigning.SignatureFilePathFor(file), signature);
        Console.WriteLine($"Signed {file}");
    }

    return 0;
}

static int Verify(Dictionary<string, string> options, List<string> files)
{
    if (files.Count == 0)
    {
        throw new ArgumentException("Give at least one assembly to verify.");
    }

    using var certificate = ModuleSigning.LoadPublicCertificate(Required(options, "cer"));
    var failures = 0;

    foreach (var file in files)
    {
        var signature = ModuleSigning.ReadSignatureFile(ModuleSigning.SignatureFilePathFor(file), out var error);
        var result = signature is null
            ? ModuleSignatureVerification.Invalid(error!)
            : ModuleSigning.Verify(Path.GetFileName(file), File.ReadAllBytes(file), signature, certificate);

        Console.WriteLine(result.IsValid ? $"OK      {file}" : $"FAILED  {file}: {result.Error}");
        failures += result.IsValid ? 0 : 1;
    }

    return failures == 0 ? 0 : 1;
}
