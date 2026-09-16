namespace ColdNet.Core.Modules;

/// <summary>
/// A single pipeline step (CNIMPORT, CNREPLACE, CNBARCODE, ...). Implementations are stateless
/// and reusable across jobs: all per-job state lives in the <see cref="ModuleExecutionContext"/>
/// passed to <see cref="ExecuteAsync"/>. Decorate implementations with
/// <see cref="ModuleDefinitionAttribute"/> so <c>ModuleRegistry</c> (ColdNet.Engine) picks them
/// up automatically.
/// </summary>
public interface IColdModule
{
    Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken);
}
