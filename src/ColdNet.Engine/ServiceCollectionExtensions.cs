using System.Reflection;
using ColdNet.Engine.Modules;
using ColdNet.Engine.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace ColdNet.Engine;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Discovers every <c>IColdModule</c>/<c>IJobImportModule</c> in the given assemblies (module
    /// plugin assemblies included), registers a <see cref="ModuleRegistry"/> singleton plus each
    /// module type itself so the scheduler can resolve module instances (with constructor
    /// injection support, e.g. for <c>EdmVaultExportModule</c>), and wires up the chain scheduler.
    /// </summary>
    public static IServiceCollection AddColdNetEngine(this IServiceCollection services, params Assembly[] moduleAssemblies)
    {
        var assemblies = moduleAssemblies.Length > 0
            ? moduleAssemblies
            : [Assembly.GetCallingAssembly()];

        var registry = new ModuleRegistry(assemblies);
        services.AddSingleton(registry);

        foreach (var entry in registry.All)
        {
            services.AddTransient(entry.ClrType);
        }

        services.AddSingleton<ChainScheduler>();

        return services;
    }
}
