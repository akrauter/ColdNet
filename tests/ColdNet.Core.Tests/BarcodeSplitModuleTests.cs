using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.GraphicsConversion;
using ColdNet.Modules.JobSeparation;
using ImageMagick;
using Microsoft.Extensions.Logging.Abstractions;
using ZXing;

namespace ColdNet.Core.Tests;

/// <summary>
/// End-to-end check that the Magick.NET-based BGRA32 pixel extraction feeds ZXing.Net a correctly-ordered buffer -
/// renders a real barcode instead of just checking the file shuffling.
/// </summary>
public class BarcodeSplitModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-barcode-test-{Guid.NewGuid():N}");

    public BarcodeSplitModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ExecuteAsync_splits_at_a_rendered_separator_barcode()
    {
        var inputPath = Path.Combine(_dir, "BATCH01.tif");

        using var separatorPage = RenderBarcodePage("JOBSEP", 200, 80);
        using var contentPage1 = new MagickImage(MagickColors.White, 200, 80);
        using var contentPage2 = new MagickImage(MagickColors.White, 200, 80);

        MagickTiff.WriteFrames(inputPath, [contentPage1, separatorPage, contentPage2]);

        var job = new Job { FilePrefix = "BATCH01", WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, FileExtension = "tif", OutputFileExtension = "tif" },
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new BarcodeSplitModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.FinishJob);
        Assert.Equal(2, result.SpawnedJobs.Count);
        Assert.Equal("BATCH01-001", result.SpawnedJobs[0].FilePrefix);
        Assert.Equal("BATCH01-002", result.SpawnedJobs[1].FilePrefix);
        Assert.True(File.Exists(Path.Combine(_dir, "BATCH01-001.tif")));
        Assert.True(File.Exists(Path.Combine(_dir, "BATCH01-002.tif")));
    }

    private static MagickImage RenderBarcodePage(string text, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new ZXing.Common.EncodingOptions { Width = width, Height = height, Margin = 4 },
        };
        var pixelData = writer.Write(text); // BGRA32 pixel buffer

        var image = new MagickImage();
        var settings = new PixelReadSettings((uint)pixelData.Width, (uint)pixelData.Height, StorageType.Char, PixelMapping.BGRA);
        image.ReadPixels(pixelData.Pixels, settings);
        image.Format = MagickFormat.Tiff;
        return image;
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
