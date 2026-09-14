using ColdNet.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Data;

public static class DbInitializer
{
    /// <summary>Applies pending migrations and seeds the built-in "default" process group, mirroring d.cold's always-present default group.</summary>
    public static async Task MigrateAndSeedAsync(ColdNetDbContext db, CancellationToken cancellationToken = default)
    {
        EnsureSqliteDirectoryExists(db);

        await db.Database.MigrateAsync(cancellationToken);

        var hasDefaultGroup = await db.ProcessGroups.AnyAsync(g => g.IsDefault, cancellationToken);
        if (!hasDefaultGroup)
        {
            db.ProcessGroups.Add(new ProcessGroup
            {
                Name = "default",
                ShortName = "default",
                Description = "Built-in default process group.",
                IsDefault = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// SQLite (unlike SQL Server) never creates a missing directory for its data file, it just
    /// fails with "unable to open database file" - bites every fresh deployment (a published
    /// release, a first `dotnet run`, ...) where the configured "data" folder doesn't exist yet.
    /// </summary>
    private static void EnsureSqliteDirectoryExists(ColdNetDbContext db)
    {
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
