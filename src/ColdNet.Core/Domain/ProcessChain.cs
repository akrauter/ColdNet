namespace ColdNet.Core.Domain;

/// <summary>
/// An ordered pipeline of modules ("process chain" / Prozesskette). Module 0 is always an import
/// module that creates jobs; every following module processes jobs handed to it by its
/// predecessor, sequentially, until the job reaches the end of the chain.
/// </summary>
public class ProcessChain
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Disabled chains are skipped entirely by the scheduler (shown greyed-out in Admin).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Started/stopped independently of <see cref="Enabled"/> - a stopped chain keeps its jobs but
    /// the scheduler will not advance them (shown in yellow in Admin).
    /// </summary>
    public bool IsRunning { get; set; } = true;

    /// <summary>
    /// Name of the worker process that should run this chain, used for load distribution across
    /// worker instances - only one worker process should ever be configured with a given name for
    /// a given chain.
    /// </summary>
    public string WorkerName { get; set; } = "default";

    /// <summary>How many ready jobs a module processes per scheduling pass.</summary>
    public int JobsPerStep { get; set; } = 10;

    public List<ModuleInstance> Modules { get; set; } = [];
}
