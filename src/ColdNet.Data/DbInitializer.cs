using ColdNet.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Data;

public static class DbInitializer
{
    /// <summary>Applies pending migrations and seeds the built-in "default" process group, mirroring d.cold's always-present default group.</summary>
    public static async Task MigrateAndSeedAsync(ColdNetDbContext db, CancellationToken cancellationToken = default)
    {
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
}
