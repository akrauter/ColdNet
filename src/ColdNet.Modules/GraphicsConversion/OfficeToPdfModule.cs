using System.Diagnostics;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.GraphicsConversion;

public class OfficeToPdfSettings
{
    /// <summary>Path to the LibreOffice binary (soffice.exe / soffice). d.cold's DCOFFICE2PDF used MS Office instead; LibreOffice's headless mode needs no license and no UI automation.</summary>
    public string LibreOfficePath { get; set; } = "soffice";

    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Converts an Office document (docx/xlsx/pptx/odt/...) to PDF via LibreOffice headless mode -
/// the ColdNet equivalent of DCOFFICE2PDF. Requires LibreOffice to be installed on the worker
/// host; see docs/MODULES.md for the external tool it depends on.
/// </summary>
[ModuleDefinition("OfficeToPdf", ModuleCategory.GraphicsConversion, "Office to PDF", "Converts an Office document to PDF via LibreOffice headless mode.", OriginalModule = "DCOFFICE2PDF", SettingsType = typeof(OfficeToPdfSettings))]
public class OfficeToPdfModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<OfficeToPdfSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        var outputDir = context.OutputDirectory;
        Directory.CreateDirectory(outputDir);

        var startInfo = new ProcessStartInfo(settings.LibreOfficePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDir);
        startInfo.ArgumentList.Add(inputPath);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(linked.Token);
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return ModuleExecutionResult.Fail($"LibreOffice exited with code {process.ExitCode}: {stderr}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ModuleExecutionResult.Fail($"LibreOffice conversion timed out after {settings.TimeoutSeconds}s.");
        }

        var producedPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + ".pdf");
        var expectedOutputPath = context.GetOutputPath("pdf");

        if (!string.Equals(producedPath, expectedOutputPath, StringComparison.OrdinalIgnoreCase) && File.Exists(producedPath))
        {
            File.Move(producedPath, expectedOutputPath, overwrite: true);
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }
}
