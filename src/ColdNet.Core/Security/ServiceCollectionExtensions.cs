using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ColdNet.Core.Security;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// A real (not secret) key baked in purely so ColdNet works out of the box with zero setup,
    /// same as its SQLite default. Every host that uses this default logs a startup warning - see
    /// <see cref="AddColdNetSecretProtection"/>. Generate a real one with
    /// <c>dotnet run --project src/ColdNet.SecretTool -- generate-key</c> and set it via
    /// "ColdNet:Encryption:Key" (environment variable / user-secrets, not committed appsettings.json)
    /// before storing anything that matters.
    /// </summary>
    public const string DefaultDevKey = "m77slRkRlz5qLQAo1Ucqw7i6Yo1GtsDOIE8bb11wgc4=";

    /// <summary>
    /// Registers the <see cref="ISecretProtector"/> singleton every ColdNet process shares
    /// (Admin, Worker, ColdNet.SecretTool) so a value encrypted by one can be decrypted by
    /// another. Reads the key from "ColdNet:Encryption:Key"; falls back to
    /// <see cref="DefaultDevKey"/> (with a warning) if unset.
    /// </summary>
    public static IServiceCollection AddColdNetSecretProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISecretProtector>(sp =>
        {
            var key = configuration["ColdNet:Encryption:Key"];
            if (string.IsNullOrWhiteSpace(key))
            {
                key = DefaultDevKey;
            }

            if (key == DefaultDevKey)
            {
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("ColdNet.Security");
                logger?.LogWarning(
                    "ColdNet is using its built-in default encryption key (ColdNet:Encryption:Key is not set). " +
                    "This key is public (it's in the source repository) - fine for local development, but every " +
                    "stored password is effectively unencrypted if this ships to production. Generate a real key " +
                    "with 'dotnet run --project src/ColdNet.SecretTool -- generate-key' and set it via an " +
                    "environment variable or user-secrets.");
            }

            try
            {
                return AesSecretProtector.FromBase64Key(key);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    "ColdNet:Encryption:Key must be a base64-encoded 256-bit (32-byte) key, e.g. from " +
                    "'dotnet run --project src/ColdNet.SecretTool -- generate-key'.");
            }
        });

        return services;
    }
}
