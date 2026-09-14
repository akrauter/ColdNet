using ColdNet.Core.Modules;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ColdNet.Modules.TextConversion;

public class TextToPdfSettings
{
    public double FontSizePt { get; set; } = 10;

    public double PageMarginMm { get; set; } = 15;

    public int LinesPerPage { get; set; } = 60;
}

/// <summary>
/// Renders a plain-text file to a simple PDF (monospace, one page per <see cref="TextToPdfSettings.LinesPerPage"/>
/// lines) - the ColdNet equivalent of DCTXT2PDF, typically used to make print-list style text
/// deliveries long-term archivable.
/// </summary>
[ModuleDefinition("TextToPdf", ModuleCategory.TextConversion, "Text to PDF", "Renders a text file to a simple monospace PDF.", OriginalModule = "DCTXT2PDF", SettingsType = typeof(TextToPdfSettings))]
public class TextToPdfModule : IColdModule
{
    static TextToPdfModule()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new SystemFontResolver();
        }
    }

    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<TextToPdfSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        var lines = await File.ReadAllLinesAsync(inputPath, cancellationToken);

        using var document = new PdfDocument();
        var font = new XFont("Courier New", settings.FontSizePt);
        var marginPt = settings.PageMarginMm * 72.0 / 25.4;

        for (var start = 0; start < lines.Length || start == 0; start += settings.LinesPerPage)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);

            var y = marginPt;
            var lineHeight = font.GetHeight() * 1.1;
            var end = Math.Min(start + settings.LinesPerPage, lines.Length);

            for (var i = start; i < end; i++)
            {
                gfx.DrawString(lines[i], font, XBrushes.Black, marginPt, y);
                y += lineHeight;
            }

            if (lines.Length == 0)
            {
                break;
            }
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }

    /// <summary>Minimal font resolver so PdfSharp 6 (which no longer depends on GDI+) can run headless.</summary>
    private sealed class SystemFontResolver : IFontResolver
    {
        public byte[]? GetFont(string faceName)
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cour.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            };

            var path = candidates.FirstOrDefault(File.Exists);
            return path is null ? null : File.ReadAllBytes(path);
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(familyName);
    }
}
