using ColdNet.Core.Domain;
using ColdNet.Data;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Engine.Tests;

public class ModuleOrderingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"coldnet-test-modorder-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Swapping_module_orders_in_chain_succeeds_without_circular_dependency()
    {
        var options = new DbContextOptionsBuilder<ColdNetDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;

        Guid chainId;
        Guid mod1Id;
        Guid mod2Id;

        await using (var db = new ColdNetDbContext(options))
        {
            await DbInitializer.MigrateAndSeedAsync(db);

            var group = await db.ProcessGroups.FirstAsync(g => g.IsDefault);
            var chain = new ProcessChain
            {
                ProcessGroupId = group.Id,
                Name = "Order Test Chain",
                WorkerName = "default",
            };

            var mod0 = new ModuleInstance { Order = 0, ModuleTypeName = "ColdImport", DisplayName = "Import" };
            var mod1 = new ModuleInstance { Order = 1, ModuleTypeName = "RenameFiles", DisplayName = "Rename" };
            var mod2 = new ModuleInstance { Order = 2, ModuleTypeName = "OfficeToPdf", DisplayName = "Convert" };

            chain.Modules.Add(mod0);
            chain.Modules.Add(mod1);
            chain.Modules.Add(mod2);

            db.ProcessChains.Add(chain);
            await db.SaveChangesAsync();

            chainId = chain.Id;
            mod1Id = mod1.Id;
            mod2Id = mod2.Id;
        }

        // Perform swap: moving mod1 down (index 1 to 2) and mod2 up (index 2 to 1)
        await using (var db = new ColdNetDbContext(options))
        {
            var a = await db.ModuleInstances.FirstAsync(m => m.Id == mod1Id);
            var b = await db.ModuleInstances.FirstAsync(m => m.Id == mod2Id);

            var orderA = a.Order;
            var orderB = b.Order;

            a.Order = -1;
            await db.SaveChangesAsync();

            b.Order = orderA;
            a.Order = orderB;
            await db.SaveChangesAsync();
        }

        // Verify orders in DB
        await using (var db = new ColdNetDbContext(options))
        {
            var modules = await db.ModuleInstances
                .Where(m => m.ProcessChainId == chainId)
                .OrderBy(m => m.Order)
                .ToListAsync();

            Assert.Equal(3, modules.Count);
            Assert.Equal("ColdImport", modules[0].ModuleTypeName);
            Assert.Equal(0, modules[0].Order);

            Assert.Equal("OfficeToPdf", modules[1].ModuleTypeName);
            Assert.Equal(1, modules[1].Order);
            Assert.Equal(mod2Id, modules[1].Id);

            Assert.Equal("RenameFiles", modules[2].ModuleTypeName);
            Assert.Equal(2, modules[2].Order);
            Assert.Equal(mod1Id, modules[2].Id);
        }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }
}
