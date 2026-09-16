namespace ColdNet.Core.Domain;

/// <summary>
/// A unit of work moving through a <see cref="ProcessChain"/>. All files that belong to the job
/// share the <see cref="FilePrefix"/> as their file name (before the extension), living in
/// <see cref="WorkDirectory"/> - same-prefix files (e.g. Rechnung.pdf + Rechnung.att) are grouped
/// into a single job.
/// </summary>
public class Job
{
    /// <summary>Synthetic primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The chain this job is being processed by.</summary>
    public Guid ProcessChainId { get; set; }

    /// <summary>
    /// The job number, used as the file name prefix for every file belonging to the job
    /// (e.g. "00106EL123317" for files 00106EL123317.$pdf / 00106EL123317.att).
    /// </summary>
    public string FilePrefix { get; set; } = string.Empty;

    /// <summary>Directory that currently holds the job's files.</summary>
    public string WorkDirectory { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Ready;

    /// <summary>
    /// Index (0-based) into the owning chain's <see cref="ProcessChain.Modules"/> list that this
    /// job is currently waiting for / being processed by. CNIMPORT is always module 0.
    /// </summary>
    public int CurrentModuleOrder { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Module order at which processing failed, kept so a retry resumes at the right step.</summary>
    public int? ErrorModuleOrder { get; set; }

    // DateTime (not DateTimeOffset): SQLite can't translate ORDER BY/comparisons on
    // DateTimeOffset columns, and every timestamp here is UTC anyway.
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Number of processing attempts recorded for the current module (for diagnostics).</summary>
    public int AttemptCount { get; set; }
}
