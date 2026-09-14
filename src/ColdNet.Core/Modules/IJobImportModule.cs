using ColdNet.Core.Domain;
using Microsoft.Extensions.Logging;

namespace ColdNet.Core.Modules;

/// <summary>
/// The module type allowed at position 0 of a chain (the DCIMPORT equivalent). Unlike a regular
/// <see cref="IColdModule"/> it has no existing job to operate on - instead it scans its configured
/// directory and reports which new jobs should be created in the database.
/// </summary>
public interface IJobImportModule
{
    Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        CancellationToken cancellationToken);
}
