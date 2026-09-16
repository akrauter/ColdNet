using System.Text.RegularExpressions;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.Case;

public enum CaseOperator
{
    Equals,
    Contains,
    Regex,
    IsEmpty,
    NotEmpty,
}

public class CaseRule
{
    public string PropertyName { get; set; } = string.Empty;

    public CaseOperator Operator { get; set; } = CaseOperator.Equals;

    public string Value { get; set; } = string.Empty;

    /// <summary>0-based module order to jump to within the current chain when this rule matches.</summary>
    public int TargetModuleOrder { get; set; }
}

public class CaseBranchSettings
{
    /// <summary>Evaluated in order; the first matching rule decides the jump target.</summary>
    public List<CaseRule> Rules { get; set; } = [];
}

/// <summary>
/// Conditionally redirects a job to a different module within the same chain based on its
/// property bag - the ColdNet equivalent of CNCASE. If no rule matches, the job simply continues
/// to the next module as usual.
/// </summary>
[ModuleDefinition("CaseBranch", ModuleCategory.Case, "Case", "Redirects a job to a different module based on conditions over its properties.", OriginalModule = "CNCASE", SettingsType = typeof(CaseBranchSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class CaseBranchModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<CaseBranchSettings>();
        var bag = await context.LoadPropertiesAsync(cancellationToken);

        foreach (var rule in settings.Rules)
        {
            var actual = bag.Get(rule.PropertyName) ?? string.Empty;

            var matches = rule.Operator switch
            {
                CaseOperator.Equals => string.Equals(actual, rule.Value, StringComparison.OrdinalIgnoreCase),
                CaseOperator.Contains => actual.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
                CaseOperator.Regex => Regex.IsMatch(actual, rule.Value),
                CaseOperator.IsEmpty => string.IsNullOrEmpty(actual),
                CaseOperator.NotEmpty => !string.IsNullOrEmpty(actual),
                _ => false,
            };

            if (matches)
            {
                return ModuleExecutionResult.Ok(jumpToModuleOrder: rule.TargetModuleOrder);
            }
        }

        return ModuleExecutionResult.Ok();
    }
}
