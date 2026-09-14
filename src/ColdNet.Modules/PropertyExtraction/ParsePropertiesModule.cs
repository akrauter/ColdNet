using System.Text.RegularExpressions;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.PropertyExtraction;

public class ParseRule
{
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>Regex applied against the input file's text (or one matching line, see <see cref="LineContains"/>).</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Regex capture group whose value is stored. 0 = whole match.</summary>
    public int GroupIndex { get; set; } = 1;

    /// <summary>If set, <see cref="Pattern"/> only runs against lines containing this substring.</summary>
    public string? LineContains { get; set; }

    /// <summary>Stores every match instead of only the first (multi-value property).</summary>
    public bool MultiValue { get; set; }
}

public class ParsePropertiesSettings
{
    public List<ParseRule> Rules { get; set; } = [];
}

/// <summary>
/// Extracts values out of a text file into the job's property bag using regular expressions -
/// the ColdNet equivalent of DCPARSE / DCVARFRTXT / DCEXTRACTLINE combined into one flexible module.
/// </summary>
[ModuleDefinition("ParseProperties", ModuleCategory.PropertyExtraction, "Parse Properties", "Extracts values from a text file into the job's property bag via regex rules.", OriginalModule = "DCPARSE", SettingsType = typeof(ParsePropertiesSettings))]
public class ParsePropertiesModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ParsePropertiesSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        var lines = await File.ReadAllLinesAsync(inputPath, cancellationToken);
        var fullText = string.Join('\n', lines);
        var bag = await context.LoadPropertiesAsync(cancellationToken);

        foreach (var rule in settings.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern))
            {
                continue;
            }

            var regex = new Regex(rule.Pattern, RegexOptions.Multiline);
            var haystack = string.IsNullOrEmpty(rule.LineContains)
                ? fullText
                : string.Join('\n', lines.Where(l => l.Contains(rule.LineContains, StringComparison.OrdinalIgnoreCase)));

            if (rule.MultiValue)
            {
                foreach (Match match in regex.Matches(haystack))
                {
                    if (match.Groups[rule.GroupIndex].Success)
                    {
                        bag.Add(rule.PropertyName, match.Groups[rule.GroupIndex].Value.Trim());
                    }
                }
            }
            else
            {
                var match = regex.Match(haystack);
                if (match.Success && match.Groups[rule.GroupIndex].Success)
                {
                    bag.Set(rule.PropertyName, match.Groups[rule.GroupIndex].Value.Trim());
                }
            }
        }

        await context.SavePropertiesAsync(bag, cancellationToken);
        return ModuleExecutionResult.Ok();
    }
}
