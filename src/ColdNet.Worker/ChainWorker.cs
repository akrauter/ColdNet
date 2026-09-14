using ColdNet.Engine.Scheduling;
using Microsoft.Extensions.Options;

namespace ColdNet.Worker;

/// <summary>
/// The ColdNet equivalent of a running "d.cold worker" process: repeatedly runs one full
/// scheduling pass over every chain assigned to this worker (see <see cref="ChainSchedulerOptions.WorkerName"/>),
/// waiting <see cref="ColdNetWorkerOptions.PollIntervalSeconds"/> between passes.
/// </summary>
public class ChainWorker(
    ChainScheduler scheduler,
    IOptions<ColdNetWorkerOptions> options,
    ILogger<ChainWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        logger.LogInformation("ColdNet worker '{WorkerName}' starting, poll interval {Interval}", options.Value.WorkerName, pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await scheduler.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error during scheduling pass");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
