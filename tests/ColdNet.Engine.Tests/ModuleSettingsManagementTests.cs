using System.Text.Json;
using ColdNet.Core.Domain;
using ColdNet.Data;
using ColdNet.Engine.Modules;
using ColdNet.Modules.GraphicsConversion;
using ColdNet.Modules.Import;
using ColdNet.Modules.TextConversion;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Engine.Tests;

public class ModuleSettingsManagementTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"coldnet-test-modsettings-{Guid.NewGuid():N}.db");

    [Fact]
    public void ModuleRegistry_discovers_settings_types_for_modules()
    {
        var registry = new ModuleRegistry([typeof(OfficeToPdfModule).Assembly]);

        var officeEntry = registry.Find("OfficeToPdf");
        Assert.NotNull(officeEntry);
        Assert.Equal(typeof(OfficeToPdfSettings), officeEntry.SettingsType);

        var importEntry = registry.Find("ColdImport");
        Assert.NotNull(importEntry);
        Assert.Equal(typeof(ColdImportSettings), importEntry.SettingsType);

        var textPdfEntry = registry.Find("TextToPdf");
        Assert.NotNull(textPdfEntry);
        Assert.Equal(typeof(TextToPdfSettings), textPdfEntry.SettingsType);
    }

    [Fact]
    public async Task Saving_and_loading_updated_module_settings_json_persists_in_database()
    {
        var options = new DbContextOptionsBuilder<ColdNetDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;

        Guid chainId;
        Guid moduleId;

        await using (var db = new ColdNetDbContext(options))
        {
            await DbInitializer.MigrateAndSeedAsync(db);

            var group = await db.ProcessGroups.FirstAsync(g => g.IsDefault);
            var chain = new ProcessChain
            {
                ProcessGroupId = group.Id,
                Name = "Test Chain",
                WorkerName = "default",
            };

            var module = new ModuleInstance
            {
                Order = 0,
                ModuleTypeName = "OfficeToPdf",
                DisplayName = "Office to PDF",
                SettingsJson = """{"LibreOfficePath":"soffice","TimeoutSeconds":120}""",
            };

            chain.Modules.Add(module);
            db.ProcessChains.Add(chain);
            await db.SaveChangesAsync();

            chainId = chain.Id;
            moduleId = module.Id;
        }

        // Simulate user editing settings form/JSON in Admin UI and saving
        await using (var db = new ColdNetDbContext(options))
        {
            var module = await db.ModuleInstances.FirstAsync(m => m.Id == moduleId);
            Assert.Equal("soffice", JsonSerializer.Deserialize<OfficeToPdfSettings>(module.SettingsJson)?.LibreOfficePath);

            var updatedSettings = new OfficeToPdfSettings
            {
                LibreOfficePath = @"C:\Program Files\LibreOffice\program\soffice.exe",
                TimeoutSeconds = 300,
            };

            var newJson = JsonSerializer.Serialize(updatedSettings, new JsonSerializerOptions { WriteIndented = true });
            module.SettingsJson = newJson;

            await db.SaveChangesAsync();
        }

        // Verify loaded data has updated settings
        await using (var db = new ColdNetDbContext(options))
        {
            var chain = await db.ProcessChains.Include(c => c.Modules).FirstAsync(c => c.Id == chainId);
            var module = chain.Modules.Single(m => m.Id == moduleId);

            var parsed = JsonSerializer.Deserialize<OfficeToPdfSettings>(module.SettingsJson);
            Assert.NotNull(parsed);
            Assert.Equal(@"C:\Program Files\LibreOffice\program\soffice.exe", parsed.LibreOfficePath);
            Assert.Equal(300, parsed.TimeoutSeconds);
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
