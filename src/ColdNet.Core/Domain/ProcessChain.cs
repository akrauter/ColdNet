namespace ColdNet.Core.Domain;

/// <summary>
/// An ordered pipeline of modules, equivalent to a d.cold "process chain" (Prozesskette). Module 0
/// is always an import module that creates jobs; every following module processes jobs handed to
/// it by its predecessor, sequentially, until the job reaches the end of the chain.
/// </summary>
public class ProcessChain
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Disabled chains are skipped entirely by the scheduler (d.cold: grey process group state).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Started/stopped independently of <see cref="Enabled"/> - a stopped chain keeps its jobs but
    /// the scheduler will not advance them (d.cold: yellow process group state).
    /// </summary>
    public bool IsRunning { get; set; } = true;

    /// <summary>
    /// Name of the worker process that should run this chain. Mirrors d.cold's per-chain worker
    /// assignment (Change worker) used for load distribution across d.cold worker instances.
    /// </summary>
    public string WorkerName { get; set; } = "default";

    /// <summary>How many ready jobs a module processes per scheduling pass (d.cold: "Jobs per step").</summary>
    public int JobsPerStep { get; set; } = 10;

    public List<ModuleInstance> Modules { get; set; } = [];
}
