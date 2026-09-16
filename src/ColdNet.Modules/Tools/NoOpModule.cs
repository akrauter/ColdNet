using ColdNet.Core.Modules;

namespace ColdNet.Modules.Tools;

/// <summary>
/// Does nothing - useful as a placeholder step while designing a chain, or as a deliberate
/// pass-through point. The ColdNet equivalent of CNNOP.
/// </summary>
[ModuleDefinition("NoOp", ModuleCategory.Tools, "No Operation", "Does nothing; useful as a placeholder while designing a chain.", OriginalModule = "CNNOP", UsesFileExtension = false, UsesOutputFileExtension = false)]
public class NoOpModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ModuleExecutionResult.Ok());
}
