using System.Diagnostics;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.GraphicsConversion;

/// <summary>
/// Which PDF/A part to conform to. Ghostscript (the conversion engine this module wraps) can only
/// guarantee "b" (basic - visual reproducibility) conformance, not "a" (tagged/accessible) or "u"
/// (Unicode text mapping), since those need document structure tagging Ghostscript doesn't do.
/// </summary>
public enum PdfAConformanceLevel
{
    PdfA1b,
    PdfA2b,
    PdfA3b,
}

public class PdfToPdfASettings
{
    /// <summary>Path to the Ghostscript executable (gswin64c.exe / gswin32c.exe on Windows, gs on Linux/macOS).</summary>
    public string GhostscriptPath { get; set; } = "gs";

    /// <summary>
    /// Path to an RGB ICC colour profile - embedded in the output as the PDF/A OutputIntent,
    /// mandatory for every PDF/A conformance level. Ghostscript ships one with every install
    /// (e.g. "&lt;install dir&gt;/iccprofiles/srgb.icc" on Windows,
    /// "/usr/share/ghostscript/&lt;version&gt;/iccprofiles/srgb.icc" on Linux) - point this at
    /// that file, or any other valid RGB ICC profile.
    /// </summary>
    public string IccProfilePath { get; set; } = string.Empty;

    public PdfAConformanceLevel ConformanceLevel { get; set; } = PdfAConformanceLevel.PdfA2b;

    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Converts a PDF to a PDF/A-conformant archival PDF via Ghostscript's <c>pdfwrite</c> device -
/// the ColdNet equivalent of DCPDF2PDF's PDF/A conversion. Requires Ghostscript (AGPL-licensed,
/// free for any use including commercial - see
/// <see href="https://www.ghostscript.com/licensing/index.html"/>) to be installed on the worker
/// host and invoked as an external process, the same pattern <c>OfficeToPdf</c> uses for
/// LibreOffice; ColdNet never bundles or links against it.
/// </summary>
[ModuleDefinition("PdfToPdfA", ModuleCategory.GraphicsConversion, "PDF to PDF/A", "Converts a PDF to a PDF/A-conformant archival PDF via Ghostscript.", OriginalModule = "DCPDF2PDF", SettingsType = typeof(PdfToPdfASettings))]
public class PdfToPdfAModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PdfToPdfASettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        if (string.IsNullOrWhiteSpace(settings.IccProfilePath) || !File.Exists(settings.IccProfilePath))
        {
            return ModuleExecutionResult.Fail(
                "PdfToPdfA requires a valid IccProfilePath (an RGB ICC colour profile, embedded as the PDF/A " +
                "OutputIntent). Ghostscript ships one - point this at '<Ghostscript install dir>/iccprofiles/srgb.icc' " +
                "(Windows) or '/usr/share/ghostscript/<version>/iccprofiles/srgb.icc' (Linux), or any other valid RGB ICC profile.");
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var prefixScriptPath = Path.Combine(Path.GetTempPath(), $"coldnet-pdfa-prefix-{Guid.NewGuid():N}.ps");
        await File.WriteAllTextAsync(prefixScriptPath, BuildOutputIntentScript(settings.IccProfilePath), cancellationToken);

        try
        {
            var startInfo = new ProcessStartInfo(settings.GhostscriptPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add($"--permit-file-read={settings.IccProfilePath}");
            startInfo.ArgumentList.Add($"-dPDFA={ConformancePart(settings.ConformanceLevel)}");
            startInfo.ArgumentList.Add("-dBATCH");
            startInfo.ArgumentList.Add("-dNOPAUSE");
            startInfo.ArgumentList.Add("-dNOOUTERSAVE");
            startInfo.ArgumentList.Add("-sColorConversionStrategy=RGB");
            startInfo.ArgumentList.Add("-sProcessColorModel=DeviceRGB");
            startInfo.ArgumentList.Add("-dPDFACompatibilityPolicy=1");
            startInfo.ArgumentList.Add("-sDEVICE=pdfwrite");
            startInfo.ArgumentList.Add($"-sOutputFile={outputPath}");
            startInfo.ArgumentList.Add(prefixScriptPath);
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
                    return ModuleExecutionResult.Fail($"Ghostscript exited with code {process.ExitCode}: {stderr}");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ModuleExecutionResult.Fail($"Ghostscript conversion timed out after {settings.TimeoutSeconds}s.");
            }
        }
        finally
        {
            File.Delete(prefixScriptPath);
        }

        if (!File.Exists(outputPath))
        {
            return ModuleExecutionResult.Fail("Ghostscript reported success but no output file was produced.");
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }

    internal static int ConformancePart(PdfAConformanceLevel level) => level switch
    {
        PdfAConformanceLevel.PdfA1b => 1,
        PdfAConformanceLevel.PdfA2b => 2,
        PdfAConformanceLevel.PdfA3b => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    /// <summary>
    /// Builds a Ghostscript "prefix" PostScript program that embeds <paramref name="iccProfilePath"/>
    /// as the PDF/A OutputIntent, using the publicly documented Adobe pdfmark operators - the
    /// standard mechanism every PDF/A-producing tool uses, Ghostscript included (its own equivalent
    /// sample is lib/PDFA_def.ps, not reused here to keep this independent of Ghostscript's own,
    /// separately AGPL-licensed, file). The GTS_PDFA1 identifier is correct for every PDF/A part
    /// (1/2/3) - the standard reuses that exact string for backward compatibility; only <see cref="ConformancePart"/>
    /// (the -dPDFA command line value) and the resulting XMP pdfaid:part actually vary by level.
    /// </summary>
    internal static string BuildOutputIntentScript(string iccProfilePath)
    {
        // PostScript string literals treat backslash as an escape character, so Windows-style
        // paths must use forward slashes here (Ghostscript's file-open accepts them on Windows too).
        var psPath = iccProfilePath.Replace('\\', '/');

        return $$"""
            %!
            [/_objdef {ColdNetIccProfile} /type /stream /OBJ pdfmark
            [{ColdNetIccProfile} <</N 3>> /PUT pdfmark
            [{ColdNetIccProfile} ({{psPath}}) (r) file /PUT pdfmark

            [/_objdef {ColdNetOutputIntent} /type /dict /OBJ pdfmark
            [{ColdNetOutputIntent} <<
              /Type /OutputIntent
              /S /GTS_PDFA1
              /DestOutputProfile {ColdNetIccProfile}
              /OutputConditionIdentifier (sRGB IEC61966-2.1)
              /Info (sRGB IEC61966-2.1)
            >> /PUT pdfmark

            [{Catalog} <</OutputIntents [ {ColdNetOutputIntent} ]>> /PUT pdfmark

            """;
    }
}
