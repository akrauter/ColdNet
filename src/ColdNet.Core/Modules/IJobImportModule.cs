using ColdNet.Core.Domain;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

namespace ColdNet.Core.Modules;

/// <summary>
/// The module type allowed at position 0 of a chain (the DCIMPORT equivalent). Unlike a regular
/// <see cref="IColdModule"/> it has no existing job to operate on - instead it scans its configured
/// directory and reports which new jobs should be created in the database.
/// </summary>
public interface IJobImportModule
{
    /// <summary>
    /// <paramref name="secretProtector"/> is for decrypting any <see cref="SensitiveValueAttribute"/>-marked
    /// fields in <paramref name="moduleInstance"/>'s settings (e.g. a remote server password) - an
    /// import module has no <c>ModuleExecutionContext</c> to get it from since it runs before any
    /// job exists, unlike <see cref="IColdModule.ExecuteAsync"/>.
    /// </summary>
    Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        ISecretProtector secretProtector,
        CancellationToken cancellationToken);
}
