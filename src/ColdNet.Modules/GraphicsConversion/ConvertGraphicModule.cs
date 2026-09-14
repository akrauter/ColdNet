using ColdNet.Core.Modules;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;

namespace ColdNet.Modules.GraphicsConversion;

public class ConvertGraphicSettings
{
    /// <summary>0-100, only used for JPEG output.</summary>
    public int JpegQuality { get; set; } = 85;
}

/// <summary>
/// Converts a single raster image between formats (TIFF/PNG/JPEG/BMP/GIF) - the ColdNet
/// equivalent of DCCONVGRAFIC. The target format is taken from the module's configured output
/// file extension.
/// </summary>
[ModuleDefinition("ConvertGraphic", ModuleCategory.GraphicsConversion, "Convert Graphic", "Converts an image between TIFF/PNG/JPEG/BMP/GIF.", OriginalModule = "DCCONVGRAFIC", SettingsType = typeof(ConvertGraphicSettings))]
public class ConvertGraphicModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ConvertGraphicSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var image = await Image.LoadAsync(inputPath, cancellationToken);

        var targetExtension = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();

        switch (targetExtension)
        {
            case "png":
                await image.SaveAsPngAsync(outputPath, cancellationToken);
                break;
            case "jpg":
            case "jpeg":
                await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = settings.JpegQuality }, cancellationToken);
                break;
            case "bmp":
                await image.SaveAsBmpAsync(outputPath, cancellationToken);
                break;
            case "gif":
                await image.SaveAsGifAsync(outputPath, cancellationToken);
                break;
            case "tif":
            case "tiff":
                await image.SaveAsTiffAsync(outputPath, new TiffEncoder(), cancellationToken);
                break;
            default:
                return ModuleExecutionResult.Fail($"Unsupported target format: .{targetExtension}");
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }
}
