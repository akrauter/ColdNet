using System.Text;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.Csv;

public class PropertiesToCsvSettings
{
    public string NameColumnHeader { get; set; } = "Name";

    public string ValueColumnHeader { get; set; } = "Value";

    /// <summary>
    /// Field delimiter - only its first character is used. ";" (the default) opens correctly in a
    /// German-locale Excel without a manual text-import step, since "," is already the decimal
    /// separator there.
    /// </summary>
    public string Delimiter { get; set; } = ";";

    public bool IncludeHeader { get; set; } = true;

    /// <summary>Adds a "DocumentType" row from the module's DMS support "Document type" field, same as <c>PropertiesToXml</c>.</summary>
    public bool IncludeDocumentType { get; set; } = true;
}

/// <summary>
/// Serializes the job's property bag to a CSV file, one row per key/value pair (Name;Value by
/// default) - the CSV counterpart of <c>PropertiesToXml</c>, for handing extracted values (e.g.
/// from <c>ExtractText</c> + <c>ParseProperties</c>) to a downstream system or a spreadsheet
/// instead of an XML index file. A multi-value field (see <c>PropertyBag</c>) produces one row per
/// value, all sharing the same Name. Written UTF-8 with a byte-order mark, since Excel otherwise
/// often misreads UTF-8 CSVs containing non-ASCII characters (e.g. "ä"/"ö"/"ü"/"ß") as Windows-1252.
/// </summary>
[ModuleDefinition("PropertiesToCsv", ModuleCategory.Csv, "Properties to CSV", "Writes the job's property bag out as a CSV file, one row per key/value pair.", SettingsType = typeof(PropertiesToCsvSettings), UsesFileExtension = false)]
public class PropertiesToCsvModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PropertiesToCsvSettings>();
        var bag = await context.LoadPropertiesAsync(cancellationToken);

        var delimiter = string.IsNullOrEmpty(settings.Delimiter) ? ';' : settings.Delimiter[0];
        var sb = new StringBuilder();

        if (settings.IncludeHeader)
        {
            AppendRow(sb, delimiter, settings.NameColumnHeader, settings.ValueColumnHeader);
        }

        if (settings.IncludeDocumentType && !string.IsNullOrWhiteSpace(context.DmsSupport.DocumentType))
        {
            AppendRow(sb, delimiter, "DocumentType", context.DmsSupport.DocumentType);
        }

        foreach (var (key, values) in bag.Values)
        {
            foreach (var value in values)
            {
                AppendRow(sb, delimiter, key, value);
            }
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);

        return ModuleExecutionResult.Ok();
    }

    private static void AppendRow(StringBuilder sb, char delimiter, string name, string value)
    {
        sb.Append(EscapeCsvField(name, delimiter)).Append(delimiter).Append(EscapeCsvField(value, delimiter)).Append("\r\n");
    }

    private static string EscapeCsvField(string field, char delimiter)
    {
        if (field.IndexOfAny([delimiter, '"', '\n', '\r']) < 0)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
