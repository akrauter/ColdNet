using ColdNet.Core.Domain;
using ColdNet.Core.ExportImport;
using ColdNet.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ColdNet.Engine.Tests;

public class ExportImportIntegrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"coldnet-test-expimp-{Guid.NewGuid():N}.db");

    private DbContextOptions<ColdNetDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ColdNetDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;

    [Fact]
    public async Task ExportAndImport_seeded_group_and_chain_into_database_succeeds()
    {
        var options = CreateOptions();

        // 1. Seed database
        await using (var db = new ColdNetDbContext(options))
        {
            await DbInitializer.MigrateAndSeedAsync(db);
        }

        string exportedGroupJson;
        string exportedChainJson;

        // 2. Export default group and "Office-zu-PDF" chain
        await using (var db = new ColdNetDbContext(options))
        {
            var defaultGroup = await db.ProcessGroups
                .Include(g => g.Chains)
                .ThenInclude(c => c.Modules)
                .SingleAsync(g => g.IsDefault);

            exportedGroupJson = ProcessExportImportService.ExportGroup(defaultGroup);

            var officeChain = await db.ProcessChains
                .Include(c => c.Modules)
                .SingleAsync(c => c.Name == "Office-zu-PDF");

            exportedChainJson = ProcessExportImportService.ExportChain(officeChain);
        }

        Assert.NotEmpty(exportedGroupJson);
        Assert.NotEmpty(exportedChainJson);

        // 3. Import Group into database
        var groupImportResult = ProcessExportImportService.Import(exportedGroupJson);
        Assert.True(groupImportResult.Success);
        Assert.NotNull(groupImportResult.Group);

        groupImportResult.Group.Name = "Cloned Group";
        groupImportResult.Group.ShortName = "CLONE";

        await using (var db = new ColdNetDbContext(options))
        {
            db.ProcessGroups.Add(groupImportResult.Group);
            await db.SaveChangesAsync();
        }

        // 4. Verify Group in database
        await using (var db = new ColdNetDbContext(options))
        {
            var clonedGroup = await db.ProcessGroups
                .Include(g => g.Chains)
                .ThenInclude(c => c.Modules)
                .SingleOrDefaultAsync(g => g.Name == "Cloned Group");

            Assert.NotNull(clonedGroup);
            Assert.False(clonedGroup.IsDefault);
            Assert.Equal(2, clonedGroup.Chains.Count);

            foreach (var chain in clonedGroup.Chains)
            {
                Assert.Equal(clonedGroup.Id, chain.ProcessGroupId);
                Assert.NotEmpty(chain.Modules);
                foreach (var mod in chain.Modules)
                {
                    Assert.Equal(chain.Id, mod.ProcessChainId);
                }
            }

            // 5. Import single Chain into Cloned Group
            var chainImportResult = ProcessExportImportService.Import(exportedChainJson, clonedGroup.Id);
            Assert.True(chainImportResult.Success);
            Assert.NotNull(chainImportResult.Chain);

            chainImportResult.Chain.Name = "Cloned Office Chain";
            db.ProcessChains.Add(chainImportResult.Chain);
            await db.SaveChangesAsync();
        }

        // 6. Verify newly imported chain in Cloned Group
        await using (var db = new ColdNetDbContext(options))
        {
            var clonedChain = await db.ProcessChains
                .Include(c => c.Modules)
                .SingleOrDefaultAsync(c => c.Name == "Cloned Office Chain");

            Assert.NotNull(clonedChain);
            Assert.Equal(4, clonedChain.Modules.Count);
            Assert.Equal("ColdImport", clonedChain.Modules[0].ModuleTypeName);
            Assert.Equal("RenameFiles", clonedChain.Modules[1].ModuleTypeName);
            Assert.Equal("OfficeToPdf", clonedChain.Modules[2].ModuleTypeName);
            Assert.Equal("FileMover", clonedChain.Modules[3].ModuleTypeName);
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
