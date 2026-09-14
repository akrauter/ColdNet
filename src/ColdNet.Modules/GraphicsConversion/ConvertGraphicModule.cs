using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using ColdNet.Core.Modules;
using SysEncoder = System.Drawing.Imaging.Encoder;

namespace ColdNet.Modules.GraphicsConversion;

public class ConvertGraphicSettings
{
    /// <summary>0-100, only used for JPEG output.</summary>
    public int JpegQuality { get; set; } = 85;
}

/// <summary>
/// Converts a single raster image between formats (TIFF/PNG/JPEG/BMP/GIF) - the ColdNet
/// equivalent of DCCONVGRAFIC. The target format is taken from the module's configured output
/// file extension. Built on <c>System.Drawing.Common</c> (MIT, Windows-only) instead of a
/// third-party imaging library to keep this module's dependencies MIT-licensed.
/// </summary>
[ModuleDefinition("ConvertGraphic", ModuleCategory.GraphicsConversion, "Convert Graphic", "Converts an image between TIFF/PNG/JPEG/BMP/GIF.", OriginalModule = "DCCONVGRAFIC", SettingsType = typeof(ConvertGraphicSettings))]
[SupportedOSPlatform("windows")]
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
        if (targetExtension is not ("png" or "jpg" or "jpeg" or "bmp" or "gif" or "tif" or "tiff"))
        {
            return ModuleExecutionResult.Fail($"Unsupported target format: .{targetExtension}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using (var image = Image.FromFile(inputPath))
        {
            switch (targetExtension)
            {
                case "png":
                    image.Save(outputPath, ImageFormat.Png);
                    break;
                case "jpg":
                case "jpeg":
                    SaveJpeg(image, outputPath, settings.JpegQuality);
                    break;
                case "bmp":
                    image.Save(outputPath, ImageFormat.Bmp);
                    break;
                case "gif":
                    image.Save(outputPath, ImageFormat.Gif);
                    break;
                default: // tif/tiff
                    image.Save(outputPath, ImageFormat.Tiff);
                    break;
            }
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }

    private static void SaveJpeg(Image image, string outputPath, int quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(SysEncoder.Quality, (long)Math.Clamp(quality, 0, 100));
        image.Save(outputPath, encoder, encoderParams);
    }
}
