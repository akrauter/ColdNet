using ColdNet.Core.Modules;

namespace ColdNet.Modules.PropertyExtraction;

public class VariableAssignment
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Literal value; supports the placeholders {JobPrefix}, {Now:format}, {DocumentType},
    /// {FileName}, {Extension}.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

public class SetVariableSettings
{
    public List<VariableAssignment> Assignments { get; set; } = [];
}

/// <summary>
/// Writes constant or computed values into the job's property bag - the ColdNet equivalent of
/// DCSETVAR/DCSETCONST.
/// </summary>
[ModuleDefinition("SetVariable", ModuleCategory.PropertyExtraction, "Set Variable", "Writes constant or computed values into the job's property bag.", OriginalModule = "DCSETVAR", SettingsType = typeof(SetVariableSettings))]
public class SetVariableModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SetVariableSettings>();
        var bag = await context.LoadPropertiesAsync(cancellationToken);

        foreach (var assignment in settings.Assignments)
        {
            bag.Set(assignment.Name, ResolvePlaceholders(assignment.Value, context));
        }

        await context.SavePropertiesAsync(bag, cancellationToken);
        return ModuleExecutionResult.Ok();
    }

    internal static string ResolvePlaceholders(string value, ModuleExecutionContext context)
    {
        var inputPath = context.GetInputPath();
        var result = value.Replace("{JobPrefix}", context.Job.FilePrefix);
        result = result.Replace("{DocumentType}", context.DmsSupport.DocumentType ?? string.Empty);
        result = result.Replace("{FileName}", Path.GetFileName(inputPath));
        result = result.Replace("{Extension}", Path.GetExtension(inputPath).TrimStart('.'));

        var now = DateTimeOffset.Now;
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\{Now(?::([^}]+))?\}", m =>
        {
            var format = m.Groups[1].Success ? m.Groups[1].Value : "yyyy-MM-dd";
            return now.ToString(format);
        });

        return result;
    }
}
