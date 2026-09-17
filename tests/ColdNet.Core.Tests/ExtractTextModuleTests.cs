using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.PropertyExtraction;
using ColdNet.Modules.TextConversion;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Core.Tests;

/// <summary>
/// Tesseract isn't installed in this test environment (nor in CI - see <c>PdfToPdfAModuleTests</c>
/// for the same situation with Ghostscript), so the OCR path is only covered for its fail-fast
/// behavior (a bad/missing Tesseract path must produce a clean Fail, not an unhandled exception -
/// this module is called directly by these tests, not only through <c>ChainScheduler</c>'s own
/// outer try/catch). The PDF text-layer path needs no external tool (PdfPig is pure managed) and
/// is fully covered here, including round-tripping a real PDF produced by <c>TextToPdfModule</c>.
/// </summary>
public class ExtractTextModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-extracttext-test-{Guid.NewGuid():N}");

    public ExtractTextModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ExecuteAsync_fails_fast_when_input_file_is_missing()
    {
        var context = CreateContext("MISSING", "pdf", "txt", "{}");

        var result = await new ExtractTextModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Input file not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_extracts_a_real_pdfs_embedded_text_layer()
    {
        // Produce a real PDF with a genuine text layer via TextToPdfModule (already covers font
        // resolution on both Windows and Linux CI), then feed it through ExtractTextModule.
        var sourceTextPath = Path.Combine(_dir, "DOC01.txt");
        await File.WriteAllTextAsync(sourceTextPath, "Invoice Number: 4711\nCustomer: Acme GmbH");

        var toPdfContext = CreateContext("DOC01", "txt", "pdf", "{}");
        var toPdfResult = await new TextToPdfModule().ExecuteAsync(toPdfContext, CancellationToken.None);
        Assert.True(toPdfResult.Success, toPdfResult.ErrorMessage);

        var extractContext = CreateContext("DOC01", "pdf", "txt", """{"MinPdfTextLength":5}""");
        var result = await new ExtractTextModule().ExecuteAsync(extractContext, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var extractedText = await File.ReadAllTextAsync(Path.Combine(_dir, "DOC01.txt"));
        Assert.Contains("Invoice Number: 4711", extractedText);
        Assert.Contains("Customer: Acme GmbH", extractedText);

        // The two source lines must stay on separate lines (a word boundary is not enough) - a
        // naive extractor that just concatenates every text-showing operation with no separator
        // would glue them into "...4711Customer:...", which still contains both substrings above
        // but silently breaks any \s-anchored regex a following ParseProperties step relies on.
        var invoiceNumberMatch = System.Text.RegularExpressions.Regex.Match(extractedText, @"Invoice Number:\s*(\S+)");
        Assert.True(invoiceNumberMatch.Success);
        Assert.Equal("4711", invoiceNumberMatch.Groups[1].Value);
    }

    [Fact]
    public void ExtractPdfText_fails_when_the_pdf_has_no_usable_text_layer()
    {
        var inputPath = Path.Combine(_dir, "BLANK.pdf");
        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.AddPage(); // a real, valid, but completely blank PDF page - no text at all
            document.Save(inputPath);
        }

        var ex = Assert.Throws<InvalidOperationException>(() => ExtractTextModule.ExtractPdfText(inputPath, minPdfTextLength: 20));

        Assert.Contains("no usable embedded text layer", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_fails_cleanly_when_tesseract_cannot_be_started()
    {
        var inputPath = Path.Combine(_dir, "SCAN01.png");
        await File.WriteAllBytesAsync(inputPath, [0x89, 0x50, 0x4E, 0x47]); // not a real PNG, never reached

        var context = CreateContext("SCAN01", "png", "txt", """{"TesseractPath":"coldnet-tesseract-does-not-exist"}""");

        var result = await new ExtractTextModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Tesseract", result.ErrorMessage);
    }

    private ModuleExecutionContext CreateContext(string filePrefix, string fileExtension, string outputExtension, string settingsJson)
    {
        var job = new Job { FilePrefix = filePrefix, WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, FileExtension = fileExtension, OutputFileExtension = outputExtension },
            SettingsJson = settingsJson,
        };
        return new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
