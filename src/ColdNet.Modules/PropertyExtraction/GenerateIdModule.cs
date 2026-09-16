using ColdNet.Core.Domain;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.PropertyExtraction;

public class GenerateIdSettings
{
    public string PropertyName { get; set; } = "id";

    /// <summary>"Guid" | "UniqueJobId" (12-char alphanumeric, same generator as CNIMPORT) | "Timestamp".</summary>
    public string Format { get; set; } = "UniqueJobId";
}

/// <summary>
/// Generates a unique identifier into the job's property bag - the ColdNet equivalent of CNGETID.
/// </summary>
[ModuleDefinition("GenerateId", ModuleCategory.PropertyExtraction, "Generate ID", "Generates a unique identifier into the job's property bag.", OriginalModule = "CNGETID", SettingsType = typeof(GenerateIdSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class GenerateIdModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GenerateIdSettings>();
        var bag = await context.LoadPropertiesAsync(cancellationToken);

        var value = settings.Format switch
        {
            "Guid" => Guid.NewGuid().ToString("N"),
            "Timestamp" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            _ => JobNumberGenerator.GenerateUniqueJobId(),
        };

        bag.Set(settings.PropertyName, value);
        await context.SavePropertiesAsync(bag, cancellationToken);
        return ModuleExecutionResult.Ok();
    }
}
