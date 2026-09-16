using ColdNet.EdmVault.FileDrop;
using ColdNet.EdmVault.RestApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ColdNet.EdmVault;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the file-drop EDMVault connector (copy/move + index file, no API required) as the default <see cref="IEdmVaultHandoverWriter"/>.</summary>
    public static IServiceCollection AddEdmVaultFileDrop(this IServiceCollection services)
    {
        services.AddSingleton<IEdmVaultHandoverWriter, FileDropEdmVaultHandoverWriter>();
        return services;
    }

    /// <summary>
    /// Registers the REST plumbing (named HttpClient, auth token provider, project-by-title
    /// resolver) talking to a live EdmVault.Api instance (see D:\_Dev\Azubiprojekte\EdmVault).
    /// Bind credentials/base URL under "ColdNet:EdmVault:RestApi". Safe to call unconditionally -
    /// REST-specific modules (<see cref="EdmVaultImportModule"/>, and <c>SftpImport</c>'s cousin
    /// for the export direction) need this regardless of which connector is the *default* writer.
    /// </summary>
    public static IServiceCollection AddEdmVaultRestApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EdmVaultRestApiOptions>(configuration.GetSection("ColdNet:EdmVault:RestApi"));

        services.AddHttpClient(EdmVaultRestApiConstants.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<EdmVaultRestApiOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            }
        });

        services.AddSingleton<EdmVaultAuthTokenProvider>();
        services.AddSingleton<EdmVaultProjectResolver>();
        return services;
    }

    /// <summary>Registers the REST connector as the default <see cref="IEdmVaultHandoverWriter"/> (on top of the plumbing from <see cref="AddEdmVaultRestApiClient"/>).</summary>
    public static IServiceCollection AddEdmVaultRestApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEdmVaultRestApiClient(configuration);
        services.AddSingleton<IEdmVaultHandoverWriter, RestApiEdmVaultHandoverWriter>();
        return services;
    }

    /// <summary>
    /// Registers the REST plumbing (always - needed by <see cref="EdmVaultImportModule"/>
    /// regardless of the chosen default) plus whichever connector "ColdNet:EdmVault:Connector"
    /// selects as the *default* export writer ("FileDrop" [default] | "RestApi").
    /// </summary>
    public static IServiceCollection AddEdmVault(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEdmVaultRestApiClient(configuration);

        var connector = configuration["ColdNet:EdmVault:Connector"] ?? "FileDrop";

        return string.Equals(connector, "RestApi", StringComparison.OrdinalIgnoreCase)
            ? services.AddSingleton<IEdmVaultHandoverWriter, RestApiEdmVaultHandoverWriter>()
            : services.AddEdmVaultFileDrop();
    }
}
