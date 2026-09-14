using ColdNet.Core.Modules;

namespace ColdNet.Engine.Modules;

/// <summary>One entry of the module catalogue, used by the admin UI's "add module" picker.</summary>
public sealed record ModuleCatalogEntry(
    string TypeName,
    ModuleCategory Category,
    string DisplayName,
    string Description,
    string? OriginalModule,
    Type ClrType,
    bool IsImportModule,
    Type? SettingsType);
