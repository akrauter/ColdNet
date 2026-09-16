using ImageMagick;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.GraphicsConversion;

public class ConvertGraphicSettings
{
    /// <summary>0-100, only used for JPEG output.</summary>
    public int JpegQuality { get; set; } = 85;
}

/// <summary>
/// Converts a single raster image between formats (TIFF/PNG/JPEG/BMP/GIF) - the ColdNet
/// equivalent of CNCONVGRAFIC. The target format is taken from the module's configured output
/// file extension. Built on <c>Magick.NET</c> (Apache-2.0, cross-platform / Linux Docker compatible).
/// </summary>
[ModuleDefinition("ConvertGraphic", ModuleCategory.GraphicsConversion, "Convert Graphic", "Converts an image between TIFF/PNG/JPEG/BMP/GIF.", OriginalModule = "CNCONVGRAFIC", SettingsType = typeof(ConvertGraphicSettings))]
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

        var outputPath = context.GetOutputPath();
        var targetExtension = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
        var format = targetExtension switch
        {
            "png" => MagickFormat.Png,
            "jpg" or "jpeg" => MagickFormat.Jpeg,
            "bmp" => MagickFormat.Bmp,
            "gif" => MagickFormat.Gif,
            "tif" or "tiff" => MagickFormat.Tiff,
            _ => MagickFormat.Unknown
        };

        if (format == MagickFormat.Unknown)
        {
            return ModuleExecutionResult.Fail($"Unsupported target format: .{targetExtension}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using (var image = new MagickImage(inputPath))
        {
            image.Format = format;
            if (format == MagickFormat.Jpeg)
            {
                image.Quality = (uint)Math.Clamp(settings.JpegQuality, 0, 100);
            }
            await image.WriteAsync(outputPath, cancellationToken);
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }
}
