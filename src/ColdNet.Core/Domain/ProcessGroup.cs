namespace ColdNet.Core.Domain;

/// <summary>
/// A container for process chains ("process group") - used purely to organize and bulk-operate
/// (start/stop) related chains in the admin UI.
/// </summary>
public class ProcessGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Up to 10 characters, shown on the group tile - falls back to the first 10 chars of Name.</summary>
    public string ShortName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>True for the built-in "default" group that cannot be deleted.</summary>
    public bool IsDefault { get; set; }

    public List<ProcessChain> Chains { get; set; } = [];
}
