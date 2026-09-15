using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.GraphicsConversion;
using ImageMagick;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Core.Tests;

/// <summary>
/// Covers the Magick.NET-based graphics modules (Apache-2.0, cross-platform / Linux Docker compatible).
/// </summary>
public class GraphicsModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-gfx-test-{Guid.NewGuid():N}");

    public GraphicsModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ConvertGraphicModule_converts_png_to_jpeg()
    {
        var inputPath = Path.Combine(_dir, "PAGE01.png");
        using (var image = new MagickImage(MagickColors.White, 10, 10))
        {
            image.Write(inputPath, MagickFormat.Png);
        }

        var job = new Job { FilePrefix = "PAGE01", WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, FileExtension = "png", OutputFileExtension = "jpg" },
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new ConvertGraphicModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        var outputPath = Path.Combine(_dir, "PAGE01.jpg");
        Assert.True(File.Exists(outputPath));
        using var converted = new MagickImage(outputPath);
        Assert.Equal(10, (int)converted.Width);
    }

    [Fact]
    public async Task MultiPageTiffModule_combines_single_page_files_into_one_multi_frame_tiff()
    {
        for (var i = 1; i <= 3; i++)
        {
            using var image = new MagickImage(MagickColors.White, 5, 5);
            image.Write(Path.Combine(_dir, $"DOC01_{i}.tif"), MagickFormat.Tiff);
        }

        var job = new Job { FilePrefix = "DOC01", WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, OutputFileExtension = "tif" },
            SettingsJson = """{"SourceFileMask":"{prefix}_*.tif"}""",
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new MultiPageTiffModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        var outputPath = Path.Combine(_dir, "DOC01.tif");
        using var combined = new MagickImageCollection(outputPath);
        Assert.Equal(3, combined.Count);
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
