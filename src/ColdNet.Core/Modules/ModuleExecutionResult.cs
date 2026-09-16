namespace ColdNet.Core.Modules;

/// <summary>A sibling job to create, used by job-separation modules (barcode/page splitting, etc.).</summary>
public sealed record NewJobRequest(string FilePrefix, string WorkDirectory);

/// <summary>
/// Outcome of running one module against one job. On failure the job is parked in
/// <see cref="Domain.JobStatus.Error"/> at the module that failed: fix the cause, reset the
/// status, and processing resumes from that module.
/// </summary>
public sealed class ModuleExecutionResult
{
    public bool Success { get; private init; }

    public string? ErrorMessage { get; private init; }

    /// <summary>Additional jobs spawned by this module (e.g. one job per separated document).</summary>
    public IReadOnlyList<NewJobRequest> SpawnedJobs { get; private init; } = [];

    /// <summary>
    /// When set, the job jumps to this module order next instead of the immediate successor -
    /// used by conditional/branching modules (the CNCASE equivalent).
    /// </summary>
    public int? JumpToModuleOrder { get; private init; }

    /// <summary>When true, the job is marked Finished immediately, skipping any remaining modules.</summary>
    public bool FinishJob { get; private init; }

    public static ModuleExecutionResult Ok(IReadOnlyList<NewJobRequest>? spawnedJobs = null, int? jumpToModuleOrder = null, bool finishJob = false) =>
        new()
        {
            Success = true,
            SpawnedJobs = spawnedJobs ?? [],
            JumpToModuleOrder = jumpToModuleOrder,
            FinishJob = finishJob,
        };

    public static ModuleExecutionResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
