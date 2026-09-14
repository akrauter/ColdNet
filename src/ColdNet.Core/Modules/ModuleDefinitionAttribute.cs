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

    /// <summary>Name of the original d.cold module this one is modelled after, e.g. "DCIMPORT". Purely informational.</summary>
    public string? OriginalModule { get; init; }

    /// <summary>
    /// The module-specific settings POCO this module reads via <c>ModuleExecutionContext.GetSettings&lt;T&gt;()</c>,
    /// if any. When set, the admin UI can seed a new module instance's settings JSON with this
    /// type's default values instead of an empty object.
    /// </summary>
    public Type? SettingsType { get; init; }
}
