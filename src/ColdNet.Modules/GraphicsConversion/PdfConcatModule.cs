using ColdNet.Core.Modules;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ColdNet.Modules.GraphicsConversion;

public class PdfConcatSettings
{
    /// <summary>Glob (relative to the input directory) matching the source PDFs, e.g. "{prefix}_*.pdf".</summary>
    public string SourceFileMask { get; set; } = "{prefix}_*.pdf";
}

/// <summary>
/// Concatenates several PDFs sharing the job's prefix into one output PDF - the ColdNet
/// equivalent of CNPDFCONCAT / CNAPPENDPDF.
/// </summary>
[ModuleDefinition("PdfConcat", ModuleCategory.GraphicsConversion, "PDF Concat", "Merges several PDFs sharing the job's prefix into one PDF.", OriginalModule = "CNPDFCONCAT", SettingsType = typeof(PdfConcatSettings), UsesFileExtension = false)]
public class PdfConcatModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PdfConcatSettings>();
        var mask = settings.SourceFileMask.Replace("{prefix}", context.Job.FilePrefix);

        var files = Directory.EnumerateFiles(context.InputDirectory, mask)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            return ModuleExecutionResult.Fail($"No source PDFs matched '{mask}' in {context.InputDirectory}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        using var target = new PdfDocument();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = PdfReader.Open(file, PdfDocumentOpenMode.Import);
            foreach (var page in source.Pages)
            {
                target.AddPage(page);
            }
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        target.Save(outputPath);

        if (context.Common.DeleteSourceFile)
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        return ModuleExecutionResult.Ok();
    }
}
