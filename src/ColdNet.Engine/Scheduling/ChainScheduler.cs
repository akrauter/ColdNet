using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Security;
using ColdNet.Data;
using ColdNet.Engine.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ColdNet.Engine.Scheduling;

/// <summary>
/// Drives one processing pass over every chain assigned to this worker, following d.cold's
/// documented scheduling order: for module position 0, 1, 2, ... process that module's ready
/// jobs across every chain before moving to the next position (Prozesskette 1/Modul 1, Prozesskette
/// 2/Modul 1, ..., Prozesskette 1/Modul 2, ...), then start over. Call <see cref="RunOnceAsync"/>
/// in a loop (see ColdNet.Worker) with a short delay between passes.
/// </summary>
public class ChainScheduler(
    IDbContextFactory<ColdNetDbContext> dbContextFactory,
    ModuleRegistry moduleRegistry,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    IOptions<ChainSchedulerOptions> options,
    ISecretProtector secretProtector,
    ILogger<ChainScheduler> logger)
{
    private readonly string _workerName = options.Value.WorkerName;

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var chains = await db.ProcessChains
            .Include(c => c.Modules)
            .Where(c => c.Enabled && c.IsRunning && c.WorkerName == _workerName)
            .ToListAsync(cancellationToken);

        if (chains.Count == 0)
        {
            return;
        }

        foreach (var chain in chains)
        {
            chain.Modules.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        var maxModuleCount = chains.Max(c => c.Modules.Count);

        for (var moduleOrder = 0; moduleOrder < maxModuleCount; moduleOrder++)
        {
            foreach (var chain in chains.OrderBy(c => c.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var moduleInstance = moduleOrder < chain.Modules.Count ? chain.Modules[moduleOrder] : null;
                if (moduleInstance is null || !moduleInstance.Enabled)
                {
                    continue;
                }

                if (moduleOrder == 0)
                {
                    await RunImportModuleAsync(db, chain, moduleInstance, cancellationToken);
                }
                else
                {
                    await RunModuleAsync(db, chain, moduleInstance, moduleOrder, cancellationToken);
                }
            }
        }
    }

    private async Task RunImportModuleAsync(ColdNetDbContext db, ProcessChain chain, ModuleInstance moduleInstance, CancellationToken ct)
    {
        var catalogEntry = moduleRegistry.Find(moduleInstance.ModuleTypeName);
        if (catalogEntry is null || !catalogEntry.IsImportModule)
        {
            logger.LogError("Module {ModuleType} at position 0 of chain {ChainName} is not a registered import module", moduleInstance.ModuleTypeName, chain.Name);
            return;
        }

        var module = (IJobImportModule)serviceProvider.GetRequiredService(catalogEntry.ClrType);
        var moduleLogger = loggerFactory.CreateLogger(catalogEntry.ClrType);

        IReadOnlyList<NewJobRequest> discovered;
        try
        {
            discovered = await module.DiscoverJobsAsync(chain, moduleInstance, moduleLogger, secretProtector, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import module {ModuleType} failed for chain {ChainName}", moduleInstance.ModuleTypeName, chain.Name);
            return;
        }

        foreach (var request in discovered)
        {
            var exists = await db.Jobs.AnyAsync(j => j.ProcessChainId == chain.Id && j.FilePrefix == request.FilePrefix, ct);
            if (exists)
            {
                continue;
            }

            var nextOrder = 1;
            db.Jobs.Add(new Job
            {
                ProcessChainId = chain.Id,
                FilePrefix = request.FilePrefix,
                WorkDirectory = request.WorkDirectory,
                Status = nextOrder >= chain.Modules.Count ? JobStatus.Finished : JobStatus.Ready,
                CurrentModuleOrder = nextOrder,
                FinishedAtUtc = nextOrder >= chain.Modules.Count ? DateTime.UtcNow : null,
            });
        }

        if (discovered.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task RunModuleAsync(ColdNetDbContext db, ProcessChain chain, ModuleInstance moduleInstance, int moduleOrder, CancellationToken ct)
    {
        var catalogEntry = moduleRegistry.Find(moduleInstance.ModuleTypeName);
        if (catalogEntry is null || catalogEntry.IsImportModule)
        {
            logger.LogError("Module {ModuleType} in chain {ChainName} is not a registered processing module", moduleInstance.ModuleTypeName, chain.Name);
            return;
        }

        var readyJobs = await db.Jobs
            .Where(j => j.ProcessChainId == chain.Id && j.Status == JobStatus.Ready && j.CurrentModuleOrder == moduleOrder)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(chain.JobsPerStep)
            .ToListAsync(ct);

        if (readyJobs.Count == 0)
        {
            return;
        }

        var moduleLogger = loggerFactory.CreateLogger(catalogEntry.ClrType);

        foreach (var job in readyJobs)
        {
            ct.ThrowIfCancellationRequested();

            job.Status = JobStatus.Working;
            job.AttemptCount++;
            await db.SaveChangesAsync(ct);

            var module = (IColdModule)serviceProvider.GetRequiredService(catalogEntry.ClrType);
            var context = new ModuleExecutionContext(job, chain, moduleInstance, moduleLogger, secretProtector);

            ModuleExecutionResult result;
            try
            {
                result = await module.ExecuteAsync(context, ct);
            }
            catch (Exception ex)
            {
                result = ModuleExecutionResult.Fail(ex.Message);
            }

            if (!result.Success)
            {
                job.Status = JobStatus.Error;
                job.ErrorMessage = result.ErrorMessage;
                job.ErrorModuleOrder = moduleOrder;
                job.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Job {Prefix} failed at module {ModuleType} ({ModuleOrder}) in chain {ChainName}: {Error}",
                    job.FilePrefix, moduleInstance.ModuleTypeName, moduleOrder, chain.Name, result.ErrorMessage);
                continue;
            }

            // Follow-up modules that don't set an explicit Directory fall back to the job's
            // WorkDirectory, so keep it in sync with wherever this module actually left its output.
            job.WorkDirectory = context.OutputDirectory;
            job.ErrorMessage = null;
            job.ErrorModuleOrder = null;

            foreach (var spawned in result.SpawnedJobs)
            {
                var spawnedNextOrder = moduleOrder + 1;
                db.Jobs.Add(new Job
                {
                    ProcessChainId = chain.Id,
                    FilePrefix = spawned.FilePrefix,
                    WorkDirectory = spawned.WorkDirectory,
                    Status = spawnedNextOrder >= chain.Modules.Count ? JobStatus.Finished : JobStatus.Ready,
                    CurrentModuleOrder = spawnedNextOrder,
                    FinishedAtUtc = spawnedNextOrder >= chain.Modules.Count ? DateTime.UtcNow : null,
                });
            }

            if (result.FinishJob)
            {
                job.Status = JobStatus.Finished;
                job.FinishedAtUtc = DateTime.UtcNow;
            }
            else
            {
                var nextOrder = result.JumpToModuleOrder ?? moduleOrder + 1;
                if (nextOrder >= chain.Modules.Count)
                {
                    job.Status = JobStatus.Finished;
                    job.FinishedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    job.Status = JobStatus.Ready;
                    job.CurrentModuleOrder = nextOrder;
                }
            }

            job.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
