namespace ColdNet.Core.Domain;

/// <summary>
/// Equivalent of d.cold's "d.3 support" tab: lets a module resolve document type / field names
/// against the target DMS instead of hard-coded field indexes. Where d.cold logs into a d.3
/// repository, ColdNet resolves this against the configured EDMVault document type catalog.
/// </summary>
public class DmsSupportSettings
{
    public bool Enabled { get; set; }

    /// <summary>The EDMVault document type (category) this module's output belongs to.</summary>
    public string? DocumentType { get; set; }
}
