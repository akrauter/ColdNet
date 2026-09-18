using ColdNet.Core.Modules;

namespace ColdNet.TestPlugin;

/// <summary>A minimal external module, used by the module-signing tests as a stand-in for a third-party plugin assembly.</summary>
[ModuleDefinition("TestPlugin", ModuleCategory.Tools, "Test Plugin", "A stand-in for an external plugin module.")]
public class TestPluginModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ModuleExecutionResult.Ok());
}
