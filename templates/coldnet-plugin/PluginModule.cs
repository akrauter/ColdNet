using ColdNet.Core.Modules;

namespace PluginTemplate;

public class PluginSettings
{
    // TODO: add this module's settings properties here, e.g.:
    // public string SomeOption { get; set; } = string.Empty;
}

/// <summary>
/// TODO: describe what this module does and when you'd use it.
/// </summary>
[ModuleDefinition("MODULE_ID", ModuleCategory.MODULE_CATEGORY, "MODULE_DISPLAY_NAME", "MODULE_DESCRIPTION", SettingsType = typeof(PluginSettings))]
public class PluginModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PluginSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"Input file not found: {inputPath}"));
        }

        // TODO: replace this starter body - it just copies the input file to the output path
        // unchanged. See an existing module (e.g. ColdNet.Modules/TextConversion/TextReplaceModule.cs)
        // for the full read -> transform -> write -> Common.Append/DeleteSourceFile pattern.
        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.Copy(inputPath, outputPath, overwrite: context.Common.Append);

        if (context.Common.DeleteSourceFile && !string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(inputPath);
        }

        return Task.FromResult(ModuleExecutionResult.Ok());
    }
}
