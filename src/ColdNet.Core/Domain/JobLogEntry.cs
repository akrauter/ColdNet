namespace ColdNet.Core.Domain;

/// <summary>
/// One recorded step in a job's processing history (one row per module attempt - import,
/// success, or failure). Independent of the job's current live state on <see cref="Job"/>, so the
/// full path a job took through its chain stays visible after it moves on, fails, or finishes.
/// </summary>
public class JobLogEntry
{
    /// <summary>Synthetic primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }

    // DateTime (not DateTimeOffset): SQLite can't translate ORDER BY/comparisons on
    // DateTimeOffset columns, and every timestamp here is UTC anyway.
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Index (0-based) into the owning chain's module list this entry is about (the import module
    /// is always 0).
    /// </summary>
    public int ModuleOrder { get; set; }

    public string ModuleTypeName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Message { get; set; }
}
