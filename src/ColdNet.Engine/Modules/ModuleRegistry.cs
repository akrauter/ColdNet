using System.Reflection;
using ColdNet.Core.Modules;

namespace ColdNet.Engine.Modules;

/// <summary>
/// The module catalogue: every <see cref="IColdModule"/>/<see cref="IJobImportModule"/>
/// implementation decorated with <see cref="ModuleDefinitionAttribute"/>, discovered once at
/// startup from the assemblies handed to <see cref="ServiceCollectionExtensions.AddColdNetModules"/>.
/// This is what makes adding a module "codeless" from the admin UI's point of view: drop a class
/// with the attribute into ColdNet.Modules (or any referenced plugin assembly) and it shows up
/// automatically, no registry edits required.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, ModuleCatalogEntry> _byTypeName;

    public ModuleRegistry(IEnumerable<Assembly> assemblies)
    {
        _byTypeName = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(t => (Type: t, Attribute: t.GetCustomAttribute<ModuleDefinitionAttribute>()))
            .Where(x => x.Attribute is not null)
            .Select(x => new ModuleCatalogEntry(
                x.Attribute!.TypeName,
                x.Attribute.Category,
                x.Attribute.DisplayName,
                x.Attribute.Description,
                x.Attribute.OriginalModule,
                x.Type,
                typeof(IJobImportModule).IsAssignableFrom(x.Type),
                x.Attribute.SettingsType,
                x.Attribute.UsesFileExtension,
                x.Attribute.UsesOutputFileExtension))
            .ToDictionary(e => e.TypeName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ModuleCatalogEntry> All => _byTypeName.Values;

    public ModuleCatalogEntry? Find(string moduleTypeName) =>
        _byTypeName.GetValueOrDefault(moduleTypeName);

    public IEnumerable<ModuleCatalogEntry> ByCategory(ModuleCategory category) =>
        _byTypeName.Values.Where(e => e.Category == category);
}
