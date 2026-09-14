using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.JobSeparation;
using Microsoft.Extensions.Logging.Abstractions;
using ZXing;

namespace ColdNet.Core.Tests;

/// <summary>
/// End-to-end check that the GDI+-based BGRA32 pixel extraction (swapped in for ImageSharp) still
/// feeds ZXing.Net a correctly-ordered buffer - a wrong byte order would fail to decode silently
/// rather than throw, so this renders a real barcode instead of just checking the file shuffling.
/// </summary>
[SupportedOSPlatform("windows")]
public class BarcodeSplitModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-barcode-test-{Guid.NewGuid():N}");

    public BarcodeSplitModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ExecuteAsync_splits_at_a_rendered_separator_barcode()
    {
        var inputPath = Path.Combine(_dir, "BATCH01.tif");

        var separatorPage = RenderBarcodePage("JOBSEP", 200, 80);
        var contentPage1 = new Bitmap(200, 80);
        var contentPage2 = new Bitmap(200, 80);

        try
        {
            SaveMultiFrameTiff(inputPath, [contentPage1, separatorPage, contentPage2]);
        }
        finally
        {
            separatorPage.Dispose();
            contentPage1.Dispose();
            contentPage2.Dispose();
        }

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

    private static Bitmap RenderBarcodePage(string text, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new ZXing.Common.EncodingOptions { Width = width, Height = height, Margin = 4 },
        };
        var pixelData = writer.Write(text); // BGRA32 pixel buffer

        var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, pixelData.Width, pixelData.Height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bmpData.Scan0, pixelData.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return bitmap;
    }

    private static void SaveMultiFrameTiff(string path, IReadOnlyList<Bitmap> pages)
    {
        var encoderInfo = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Tiff.Guid);

        using var saveParams = new EncoderParameters(2);
        saveParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionLZW);
        saveParams.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        pages[0].Save(path, encoderInfo, saveParams);

        for (var i = 1; i < pages.Count; i++)
        {
            using var frameParams = new EncoderParameters(1);
            frameParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);
            pages[0].SaveAdd(pages[i], frameParams);
        }

        using var flushParams = new EncoderParameters(1);
        flushParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
        pages[0].SaveAdd(flushParams);
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
