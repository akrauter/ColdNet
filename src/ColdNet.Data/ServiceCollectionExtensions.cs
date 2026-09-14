using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ColdNet.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ColdNetDbContext"/> against SQLite (default - zero setup, good for a
    /// single worker) or SQL Server, chosen via the "ColdNet:Database:Provider" configuration key
    /// ("Sqlite" | "SqlServer") and the matching connection string, mirroring the database choice
    /// d.cold itself requires (SQL Server / Oracle / DB2).
    /// </summary>
    public static IServiceCollection AddColdNetDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["ColdNet:Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("ColdNet")
                                ?? "Data Source=coldnet.db";

        // AddDbContextFactory also registers ColdNetDbContext itself as a scoped service, so
        // Blazor components can keep injecting it directly while the worker's background loop
        // uses the factory to create a short-lived context per scheduling pass.
        services.AddDbContextFactory<ColdNetDbContext>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        return services;
    }
}
