using ColdNet.Core.Domain;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

namespace ColdNet.Core.Modules;

/// <summary>
/// The module type allowed at position 0 of a chain (the CNIMPORT equivalent). Unlike a regular
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
    /// <paramref name="existingFilePrefixes"/> holds every <c>FilePrefix</c> already used by this
    /// chain (a job's <c>FilePrefix</c> is unique per chain, and finished jobs are kept forever, so
    /// a naively-reused prefix - e.g. re-importing a same-named file with "Generate unique job ID"
    /// off - would collide). Pass the chosen job number through <see cref="JobNumberGenerator.MakeUnique"/>
    /// against this set *before* renaming/moving any file to match it, so the physical file and the
    /// resulting <see cref="NewJobRequest.FilePrefix"/> always agree.
    /// </summary>
    Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        ISecretProtector secretProtector,
        ISet<string> existingFilePrefixes,
        CancellationToken cancellationToken);
}
