using ColdNet.Core.Modules;

namespace ColdNet.Modules.PropertyExtraction;

public class VariableAssignment
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Literal value; supports the placeholders {JobPrefix}, {Now:format}, {DocumentType},
    /// {FileName}, {FileNameWithoutExtension}, {Extension}.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

public class SetVariableSettings
{
    public List<VariableAssignment> Assignments { get; set; } = [];
}

/// <summary>
/// Writes constant or computed values into the job's property bag - the ColdNet equivalent of
/// CNSETVAR/CNSETCONST.
/// </summary>
[ModuleDefinition("SetVariable", ModuleCategory.PropertyExtraction, "Set Variable", "Writes constant or computed values into the job's property bag.", OriginalModule = "CNSETVAR", SettingsType = typeof(SetVariableSettings), UsesOutputFileExtension = false)]
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

    internal static string ResolvePlaceholders(string value, ModuleExecutionContext context) =>
        ResolvePlaceholders(value, context, context.GetInputPath());

    /// <summary>
    /// Same placeholder set as the no-path overload, but <c>{FileName}</c>/<c>{FileNameWithoutExtension}</c>/
    /// <c>{Extension}</c> are derived from <paramref name="sourceFilePath"/> instead of always
    /// <see cref="ModuleExecutionContext.GetInputPath"/> - needed by callers (e.g. RenameFiles)
    /// that discover a job's actual file(s) on disk rather than relying on one fixed configured
    /// extension.
    /// </summary>
    internal static string ResolvePlaceholders(string value, ModuleExecutionContext context, string sourceFilePath)
    {
        var result = value.Replace("{JobPrefix}", context.Job.FilePrefix);
        result = result.Replace("{DocumentType}", context.DmsSupport.DocumentType ?? string.Empty);
        result = result.Replace("{FileName}", Path.GetFileName(sourceFilePath));
        result = result.Replace("{FileNameWithoutExtension}", Path.GetFileNameWithoutExtension(sourceFilePath));
        result = result.Replace("{Extension}", Path.GetExtension(sourceFilePath).TrimStart('.'));

        var now = DateTimeOffset.Now;
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\{Now(?::([^}]+))?\}", m =>
        {
            var format = m.Groups[1].Success ? m.Groups[1].Value : "yyyy-MM-dd";
            return now.ToString(format);
        });

        return result;
    }
}
