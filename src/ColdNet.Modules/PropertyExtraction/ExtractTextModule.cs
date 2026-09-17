using System.Diagnostics;
using System.Text;
using ColdNet.Core.Modules;
using UglyToad.PdfPig;

namespace ColdNet.Modules.PropertyExtraction;

public class ExtractTextSettings
{
    /// <summary>Path to the Tesseract OCR executable, used for image input. "tesseract" if it's
    /// on PATH.</summary>
    public string TesseractPath { get; set; } = "tesseract";

    /// <summary>Tesseract language code(s), e.g. "eng", "deu", "deu+eng" - must match a language
    /// pack actually installed alongside Tesseract.</summary>
    public string OcrLanguage { get; set; } = "deu";

    /// <summary>
    /// Minimum number of characters a PDF's embedded text layer must contain to be trusted as
    /// real, extractable text. Below this the PDF is treated as having no usable text layer (e.g.
    /// a scanned document with no OCR'd text) and the module fails with a clear message instead of
    /// silently producing near-empty output.
    /// </summary>
    public int MinPdfTextLength { get; set; } = 20;

    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Extracts plain text from a PDF or an image file, so a following <c>ParseProperties</c> step can
/// pull key/value pairs out of it with its usual regex rules - the ColdNet equivalent of
/// CNPDF2TXT/CNOCR/CNOCRTEXT. PDFs are read directly via PdfPig (Apache-2.0, pure managed, no
/// external tool needed - their embedded text layer, e.g. from a chain's own TextToPdf/OfficeToPdf
/// output); image files are OCR'd via a locally installed Tesseract (Apache-2.0), invoked as an
/// external process the same way OfficeToPdf/PdfToPdfA invoke LibreOffice/Ghostscript - ColdNet
/// never bundles or links against it.
///
/// Scanned PDFs (no embedded text layer) are NOT read directly - convert their pages to image
/// files first (e.g. Ghostscript/ImageMagick) and run this module against those instead; PDF input
/// with too little extractable text fails with a message saying so rather than silently returning
/// near-empty text.
/// </summary>
[ModuleDefinition("ExtractText", ModuleCategory.PropertyExtraction, "Extract Text", "Extracts text from a PDF's text layer, or via OCR from an image, for a following Parse Properties step.", OriginalModule = "CNPDF2TXT", SettingsType = typeof(ExtractTextSettings))]
public class ExtractTextModule : IColdModule
{
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { "pdf" };

    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ExtractTextSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        var extension = Path.GetExtension(inputPath).TrimStart('.');
        string text;

        if (PdfExtensions.Contains(extension))
        {
            try
            {
                text = ExtractPdfText(inputPath, settings.MinPdfTextLength);
            }
            catch (Exception ex)
            {
                return ModuleExecutionResult.Fail(ex.Message);
            }
        }
        else
        {
            var ocrResult = await OcrImageAsync(inputPath, settings, cancellationToken);
            if (!ocrResult.Success)
            {
                return ModuleExecutionResult.Fail(ocrResult.ErrorMessage!);
            }

            text = ocrResult.Text!;
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, text, cancellationToken);

        if (context.Common.DeleteSourceFile && !string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }

    internal static string ExtractPdfText(string inputPath, int minPdfTextLength)
    {
        using var document = PdfDocument.Open(inputPath);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        var text = sb.ToString();
        if (text.Trim().Length < minPdfTextLength)
        {
            throw new InvalidOperationException(
                $"PDF has no usable embedded text layer (found {text.Trim().Length} character(s), need at least {minPdfTextLength}) - " +
                "this module doesn't OCR scanned PDFs directly; convert the page(s) to image files and run this module against those instead.");
        }

        return text;
    }

    private static async Task<(bool Success, string? Text, string? ErrorMessage)> OcrImageAsync(string inputPath, ExtractTextSettings settings, CancellationToken cancellationToken)
    {
        // Tesseract appends ".txt" itself to whatever output base path it's given.
        var outputBase = Path.Combine(Path.GetTempPath(), $"coldnet-ocr-{Guid.NewGuid():N}");
        var outputFile = outputBase + ".txt";

        var startInfo = new ProcessStartInfo(settings.TesseractPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add(outputBase);
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(settings.OcrLanguage) ? "eng" : settings.OcrLanguage);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return (false, null, $"Could not start Tesseract at '{settings.TesseractPath}': {ex.Message}");
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(linked.Token);
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return (false, null, $"Tesseract exited with code {process.ExitCode}: {stderr}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, null, $"Tesseract OCR timed out after {settings.TimeoutSeconds}s.");
        }

        if (!File.Exists(outputFile))
        {
            return (false, null, $"Tesseract reported success but no output file was produced: {outputFile}");
        }

        try
        {
            var text = await File.ReadAllTextAsync(outputFile, cancellationToken);
            return (true, text, null);
        }
        finally
        {
            File.Delete(outputFile);
        }
    }
}
