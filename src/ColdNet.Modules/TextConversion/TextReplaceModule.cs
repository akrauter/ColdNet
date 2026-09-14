using System.Text.RegularExpressions;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.TextConversion;

public class TextReplaceRule
{
    public string Find { get; set; } = string.Empty;

    public string Replace { get; set; } = string.Empty;

    public bool IsRegex { get; set; }
}

public class TextReplaceSettings
{
    public List<TextReplaceRule> Rules { get; set; } = [];
}

/// <summary>
/// Replaces characters/strings in a text file - the ColdNet equivalent of DCCHANGE/DCREPLACE.
/// Rules run in order, each against the output of the previous one.
/// </summary>
[ModuleDefinition("TextReplace", ModuleCategory.TextConversion, "Text Replace", "Replaces text or regex patterns in a text file.", OriginalModule = "DCREPLACE", SettingsType = typeof(TextReplaceSettings))]
public class TextReplaceModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<TextReplaceSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        var text = await File.ReadAllTextAsync(inputPath, cancellationToken);

        foreach (var rule in settings.Rules)
        {
            text = rule.IsRegex
                ? Regex.Replace(text, rule.Find, rule.Replace)
                : text.Replace(rule.Find, rule.Replace);
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (context.Common.Append && File.Exists(outputPath))
        {
            await File.AppendAllTextAsync(outputPath, text, cancellationToken);
        }
        else
        {
            await File.WriteAllTextAsync(outputPath, text, cancellationToken);
        }

        if (context.Common.DeleteSourceFile && !string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }
}
