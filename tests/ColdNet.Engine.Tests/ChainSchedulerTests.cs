using ColdNet.Core.Domain;
using ColdNet.Core.Security;
using ColdNet.Data;
using ColdNet.Engine;
using ColdNet.Engine.Scheduling;
using ColdNet.Modules.Import;
using ColdNet.Modules.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColdNet.Engine.Tests;

public class ChainSchedulerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"coldnet-test-{Guid.NewGuid():N}.db");
    private readonly string _importDir = Path.Combine(Path.GetTempPath(), $"coldnet-test-import-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunOnceAsync_imports_a_file_and_advances_the_job_through_the_chain()
    {
        Directory.CreateDirectory(_importDir);
        await File.WriteAllTextAsync(Path.Combine(_importDir, "invoice.pdf"), "dummy");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ColdNetDbContext>(o => o.UseSqlite($"Data Source={_dbPath};Pooling=False"));
        services.AddColdNetEngine(typeof(ColdImportModule).Assembly);
        services.AddSingleton<ISecretProtector>(NullSecretProtector.Instance);
        services.Configure<ChainSchedulerOptions>(o => o.WorkerName = "default");

        await using var provider = services.BuildServiceProvider();
        var dbFactory = provider.GetRequiredService<IDbContextFactory<ColdNetDbContext>>();

        Guid chainId;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();

            var group = new ProcessGroup { Name = "default", ShortName = "default", IsDefault = true };
            var chain = new ProcessChain { ProcessGroupId = group.Id, Name = "test-chain", WorkerName = "default" };
            chainId = chain.Id;

            chain.Modules.Add(new ModuleInstance
            {
                ProcessChainId = chain.Id,
                Order = 0,
                ModuleTypeName = "ColdImport",
                CommonSettings = new CommonModuleSettings { Directory = _importDir },
                SettingsJson = """{"FileMask":"*.pdf","GenerateUniqueJobId":false}""",
            });
            chain.Modules.Add(new ModuleInstance
            {
                ProcessChainId = chain.Id,
                Order = 1,
                ModuleTypeName = "NoOp",
            });

            db.ProcessGroups.Add(group);
            db.ProcessChains.Add(chain);
            await db.SaveChangesAsync();
        }

        var scheduler = provider.GetRequiredService<ChainScheduler>();
        await scheduler.RunOnceAsync(CancellationToken.None);

        await using var verifyDb = await dbFactory.CreateDbContextAsync();
        var job = await verifyDb.Jobs.SingleAsync(j => j.ProcessChainId == chainId);

        Assert.Equal("invoice", job.FilePrefix);
        Assert.Equal(JobStatus.Finished, job.Status);
        Assert.True(File.Exists(Path.Combine(_importDir, "invoice.$pdf")));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // best effort - a lingering pooled connection shouldn't fail the test run
        }

        if (Directory.Exists(_importDir))
        {
            Directory.Delete(_importDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
