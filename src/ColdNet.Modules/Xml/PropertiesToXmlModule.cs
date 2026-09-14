using System.Xml.Linq;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.Xml;

public class PropertiesToXmlSettings
{
    public string RootElementName { get; set; } = "document";

    public string FieldElementName { get; set; } = "field";

    /// <summary>Written as an attribute on every field element, e.g. "name".</summary>
    public string NameAttribute { get; set; } = "name";

    public bool IncludeDocumentType { get; set; } = true;
}

/// <summary>
/// Serializes the job's property bag to an XML index file - the ColdNet equivalent of DCCONVXML
/// used to hand structured index data to downstream systems.
/// </summary>
[ModuleDefinition("PropertiesToXml", ModuleCategory.Xml, "Properties to XML", "Writes the job's property bag out as an XML index file.", OriginalModule = "DCCONVXML", SettingsType = typeof(PropertiesToXmlSettings))]
public class PropertiesToXmlModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PropertiesToXmlSettings>();
        var bag = await context.LoadPropertiesAsync(cancellationToken);

        var root = new XElement(settings.RootElementName);

        if (settings.IncludeDocumentType && !string.IsNullOrWhiteSpace(context.DmsSupport.DocumentType))
        {
            root.SetAttributeValue("documentType", context.DmsSupport.DocumentType);
        }

        foreach (var (key, values) in bag.Values)
        {
            foreach (var value in values)
            {
                root.Add(new XElement(settings.FieldElementName, new XAttribute(settings.NameAttribute, key), value));
            }
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var stream = File.Create(outputPath);
        await new XDocument(root).SaveAsync(stream, SaveOptions.None, cancellationToken);

        return ModuleExecutionResult.Ok();
    }
}
