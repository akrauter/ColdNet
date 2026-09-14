using ColdNet.EdmVault.FileDrop;
using ColdNet.EdmVault.RestApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ColdNet.EdmVault;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the file-drop EDMVault connector (copy/move + index file, no API required).</summary>
    public static IServiceCollection AddEdmVaultFileDrop(this IServiceCollection services)
    {
        services.AddSingleton<IEdmVaultHandoverWriter, FileDropEdmVaultHandoverWriter>();
        return services;
    }

    /// <summary>
    /// Registers the REST connector talking to a live EdmVault.Api instance (see
    /// D:\_Dev\Azubiprojekte\EdmVault). Bind credentials/base URL under "ColdNet:EdmVault:RestApi".
    /// </summary>
    public static IServiceCollection AddEdmVaultRestApi(this IServiceCollection services, IConfiguration configuration)
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
        services.AddSingleton<IEdmVaultHandoverWriter, RestApiEdmVaultHandoverWriter>();
        return services;
    }

    /// <summary>
    /// Registers whichever EDMVault connector "ColdNet:EdmVault:Connector" selects
    /// ("FileDrop" [default] | "RestApi").
    /// </summary>
    public static IServiceCollection AddEdmVault(this IServiceCollection services, IConfiguration configuration)
    {
        var connector = configuration["ColdNet:EdmVault:Connector"] ?? "FileDrop";

        return string.Equals(connector, "RestApi", StringComparison.OrdinalIgnoreCase)
            ? services.AddEdmVaultRestApi(configuration)
            : services.AddEdmVaultFileDrop();
    }
}
