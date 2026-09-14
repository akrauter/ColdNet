using System.Diagnostics;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.Tools;

public class ShellExecuteSettings
{
    public string Executable { get; set; } = string.Empty;

    /// <summary>
    /// Arguments with placeholders {input}, {output}, {prefix}, {inputDir}, {outputDir}
    /// substituted before the process is started.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;

    public bool FailOnNonZeroExitCode { get; set; } = true;
}

/// <summary>
/// Runs an external command line tool against the job's file - the ColdNet equivalent of
/// DCSHELLEXEC, used to bolt on any converter that has no dedicated module (Ghostscript,
/// LibreOffice, a customer script, ...).
/// </summary>
[ModuleDefinition("ShellExecute", ModuleCategory.Tools, "Shell Execute", "Runs an external command line tool against the job's file.", OriginalModule = "DCSHELLEXEC", SettingsType = typeof(ShellExecuteSettings))]
public class ShellExecuteModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ShellExecuteSettings>();

        if (string.IsNullOrWhiteSpace(settings.Executable))
        {
            return ModuleExecutionResult.Fail("No executable configured.");
        }

        var inputPath = context.GetInputPath();
        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var arguments = settings.Arguments
            .Replace("{input}", inputPath)
            .Replace("{output}", outputPath)
            .Replace("{prefix}", context.Job.FilePrefix)
            .Replace("{inputDir}", context.InputDirectory)
            .Replace("{outputDir}", context.OutputDirectory);

        var startInfo = new ProcessStartInfo(settings.Executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(linked.Token);
            var stderr = await stderrTask;

            if (settings.FailOnNonZeroExitCode && process.ExitCode != 0)
            {
                return ModuleExecutionResult.Fail($"{settings.Executable} exited with code {process.ExitCode}: {stderr}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return ModuleExecutionResult.Fail($"{settings.Executable} timed out after {settings.TimeoutSeconds}s.");
        }

        return ModuleExecutionResult.Ok();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
