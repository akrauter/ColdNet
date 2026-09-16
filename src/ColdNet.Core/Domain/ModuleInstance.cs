namespace ColdNet.Core.Domain;

/// <summary>
/// One configured step ("module instance") inside a <see cref="ProcessChain"/>, each with its own
/// isolated, per-instance configuration stored as a JSON settings blob.
/// </summary>
public class ModuleInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessChainId { get; set; }

    /// <summary>0-based position within the chain. Module 0 must always be an import module.</summary>
    public int Order { get; set; }

    /// <summary>
    /// The registered <c>IColdModule.ModuleTypeName</c> this instance runs, e.g. "ColdImport",
    /// "TextReplace", "BarcodeSplit" - the ColdNet analogues of CNIMPORT, CNREPLACE, CNBARCODE.
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>Free-text label shown in the admin UI, e.g. "Import Rechnungen".</summary>
    public string? DisplayName { get; set; }

    public bool Enabled { get; set; } = true;

    public CommonModuleSettings CommonSettings { get; set; } = new();

    public DmsSupportSettings DmsSupport { get; set; } = new();

    /// <summary>
    /// Module-specific settings, serialized as JSON. Each module defines its own settings POCO
    /// and reads it back via <c>ModuleExecutionContext.GetSettings&lt;T&gt;()</c>.
    /// </summary>
    public string SettingsJson { get; set; } = "{}";
}
