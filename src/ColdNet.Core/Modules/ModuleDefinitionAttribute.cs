using ColdNet.Core.Domain;

namespace ColdNet.Core.Modules;

/// <summary>
/// Declares catalogue metadata for an <see cref="IColdModule"/> implementation so the admin UI and
/// module registry can list it without any manual registration - drop a class with this attribute
/// into ColdNet.Modules (or a plugin assembly) and it shows up as an available pipeline step.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleDefinitionAttribute(string typeName, ModuleCategory category, string displayName, string description) : Attribute
{
    /// <summary>Stable identifier stored in <c>ModuleInstance.ModuleTypeName</c>, e.g. "ColdImport".</summary>
    public string TypeName { get; } = typeName;

    public ModuleCategory Category { get; } = category;

    public string DisplayName { get; } = displayName;

    public string Description { get; } = description;

    /// <summary>ColdNet's own historical reference code for this module, e.g. "CNIMPORT". Purely informational.</summary>
    public string? OriginalModule { get; init; }

    /// <summary>
    /// The module-specific settings POCO this module reads via <c>ModuleExecutionContext.GetSettings&lt;T&gt;()</c>,
    /// if any. When set, the admin UI can seed a new module instance's settings JSON with this
    /// type's default values instead of an empty object.
    /// </summary>
    public Type? SettingsType { get; init; }

    /// <summary>
    /// Whether this module reads <see cref="CommonModuleSettings.FileExtension"/> (directly, or
    /// via <c>ModuleExecutionContext.GetInputPath()</c> without an override). Defaults to true;
    /// set false so the admin UI hides a "General" tab field that would have no effect for this
    /// module - e.g. modules driven entirely by a <c>SourceFileMask</c>/glob instead, or with no
    /// single input file at all (import modules, property-bag-only modules).
    /// </summary>
    public bool UsesFileExtension { get; init; } = true;

    /// <summary>Same as <see cref="UsesFileExtension"/>, but for <see cref="CommonModuleSettings.OutputFileExtension"/>.</summary>
    public bool UsesOutputFileExtension { get; init; } = true;
}
