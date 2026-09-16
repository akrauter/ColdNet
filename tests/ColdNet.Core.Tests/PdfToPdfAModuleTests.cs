using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.GraphicsConversion;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Core.Tests;

/// <summary>
/// Ghostscript isn't installed in this test environment (nor in CI - see <c>OfficeToPdfModule</c>'s
/// LibreOffice for the same situation), so these cover what's verifiable without it: every
/// fail-fast path, and the two pure functions that decide what gets passed to Ghostscript.
/// The actual conversion was verified manually against a real, locally installed Ghostscript
/// 10.08.0: ran the module end to end (via <c>ModuleExecutionContext</c>, not just the raw
/// command line) against a PDF produced by <c>TextToPdfModule</c>, and confirmed the output
/// carries both a Catalog <c>/OutputIntents</c> entry and XMP <c>pdfaid:part='2' pdfaid:conformance='B'</c>
/// for <see cref="PdfAConformanceLevel.PdfA2b"/> (and 1/3 for the other two levels) - a genuinely
/// PDF/A-conformant file, not just a file that runs without error.
/// </summary>
public class PdfToPdfAModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-pdfa-test-{Guid.NewGuid():N}");

    public PdfToPdfAModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ExecuteAsync_fails_fast_when_input_file_is_missing()
    {
        var job = new Job { FilePrefix = "MISSING", WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, FileExtension = "pdf" },
            SettingsJson = """{"IccProfilePath":"C:\\fake\\srgb.icc"}""",
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new PdfToPdfAModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Input file not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_fails_fast_when_icc_profile_path_is_unset()
    {
        var inputPath = Path.Combine(_dir, "DOC01.pdf");
        File.WriteAllText(inputPath, "not a real pdf, but IccProfilePath is checked first");

        var job = new Job { FilePrefix = "DOC01", WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, FileExtension = "pdf" },
            SettingsJson = "{}", // no IccProfilePath configured
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new PdfToPdfAModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("IccProfilePath", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_fails_fast_when_icc_profile_path_does_not_exist()
    {
        var inputPath = Path.Combine(_dir, "DOC01.pdf");
        File.WriteAllText(inputPath, "not a real pdf, but IccProfilePath is checked first");

        var job = new Job { FilePrefix = "DOC01", WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, FileExtension = "pdf" },
            SettingsJson = """{"IccProfilePath":"C:\\does\\not\\exist\\srgb.icc"}""",
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new PdfToPdfAModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("IccProfilePath", result.ErrorMessage);
    }

    [Theory]
    [InlineData(PdfAConformanceLevel.PdfA1b, 1)]
    [InlineData(PdfAConformanceLevel.PdfA2b, 2)]
    [InlineData(PdfAConformanceLevel.PdfA3b, 3)]
    public void ConformancePart_maps_each_level_to_its_PDFA_part_number(PdfAConformanceLevel level, int expectedPart)
    {
        Assert.Equal(expectedPart, PdfToPdfAModule.ConformancePart(level));
    }

    [Fact]
    public void BuildOutputIntentScript_embeds_the_icc_path_with_forward_slashes_and_the_pdfa_output_intent_pdfmarks()
    {
        var script = PdfToPdfAModule.BuildOutputIntentScript(@"C:\gs\iccprofiles\srgb.icc");

        Assert.Contains("(C:/gs/iccprofiles/srgb.icc) (r) file", script);
        Assert.Contains("/Type /OutputIntent", script);
        Assert.Contains("/S /GTS_PDFA1", script);
        Assert.Contains("{Catalog} <</OutputIntents", script);
        Assert.DoesNotContain(@"\g", script); // no stray backslash left for PostScript to misparse as an escape
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
