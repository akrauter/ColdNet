using ColdNet.Core.Domain;
using ColdNet.Data;
using ColdNet.Engine.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColdNet.Engine.Tests;

public class SampleWorkflowTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"coldnet-test-sample-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task MigrateAndSeedAsync_creates_sample_office_and_text_workflows()
    {
        var options = new DbContextOptionsBuilder<ColdNetDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;

        await using (var db = new ColdNetDbContext(options))
        {
            await DbInitializer.MigrateAndSeedAsync(db);
        }

        await using (var db = new ColdNetDbContext(options))
        {
            var defaultGroup = await db.ProcessGroups
                .Include(g => g.Chains)
                .ThenInclude(c => c.Modules)
                .FirstOrDefaultAsync(g => g.IsDefault);

            Assert.NotNull(defaultGroup);
            Assert.Contains(defaultGroup.Chains, c => c.Name == "Office-zu-PDF");
            Assert.Contains(defaultGroup.Chains, c => c.Name == "Text-zu-PDF");

            var officeChain = defaultGroup.Chains.Single(c => c.Name == "Office-zu-PDF");
            Assert.Equal(4, officeChain.Modules.Count);
            Assert.Equal("ColdImport", officeChain.Modules[0].ModuleTypeName);
            Assert.Equal("RenameFiles", officeChain.Modules[1].ModuleTypeName);
            Assert.Equal("OfficeToPdf", officeChain.Modules[2].ModuleTypeName);
            Assert.False(officeChain.Modules[2].CommonSettings.DeleteSourceFile);
            Assert.Equal("FileMover", officeChain.Modules[3].ModuleTypeName);
            Assert.Equal("data/output/office", officeChain.Modules[3].CommonSettings.OutputDirectory);
        }
    }

    [Fact]
    public async Task Sample_text_to_pdf_workflow_processes_document_and_moves_both_source_and_pdf_to_output()
    {
        var importDir = Path.Combine(Path.GetTempPath(), $"coldnet-test-in-{Guid.NewGuid():N}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"coldnet-test-out-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(importDir);
            await File.WriteAllTextAsync(Path.Combine(importDir, "document1.txt"), "Sample Text Content");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<ColdNetDbContext>(o => o.UseSqlite($"Data Source={_dbPath};Pooling=False"));
            services.AddColdNetEngine(typeof(ColdNet.Modules.Import.ColdImportModule).Assembly);
            services.Configure<ChainSchedulerOptions>(o => o.WorkerName = "default");

            await using var provider = services.BuildServiceProvider();
            var dbFactory = provider.GetRequiredService<IDbContextFactory<ColdNetDbContext>>();

            Guid chainId;
            await using (var db = await dbFactory.CreateDbContextAsync())
            {
                await DbInitializer.MigrateAndSeedAsync(db);

                var chain = await db.ProcessChains
                    .Include(c => c.Modules)
                    .SingleAsync(c => c.Name == "Text-zu-PDF");

                chainId = chain.Id;

                // Configure test directories
                chain.Modules[0].CommonSettings.Directory = importDir;
                chain.Modules[3].CommonSettings.OutputDirectory = outputDir;
                await db.SaveChangesAsync();
            }

            var scheduler = provider.GetRequiredService<ChainScheduler>();
            await scheduler.RunOnceAsync(CancellationToken.None);

            await using (var verifyDb = await dbFactory.CreateDbContextAsync())
            {
                var job = await verifyDb.Jobs.SingleAsync(j => j.ProcessChainId == chainId);
                Assert.Equal(JobStatus.Finished, job.Status);
                Assert.Equal("document1", job.FilePrefix);
            }

            Assert.True(File.Exists(Path.Combine(outputDir, "document1.txt")), "Source document was not moved to output directory");
            Assert.True(File.Exists(Path.Combine(outputDir, "document1.pdf")), "Converted PDF was not moved to output directory");
        }
        finally
        {
            if (Directory.Exists(importDir)) Directory.Delete(importDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
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
