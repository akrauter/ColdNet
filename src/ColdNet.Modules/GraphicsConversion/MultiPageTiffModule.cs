using ImageMagick;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.GraphicsConversion;

public class MultiPageTiffSettings
{
    /// <summary>Glob (relative to the input directory) matching the single-page source files, e.g. "{prefix}_*.tif".</summary>
    public string SourceFileMask { get; set; } = "{prefix}_*.tif";
}

/// <summary>
/// Combines several single-page images sharing the job's prefix into one multi-page TIFF - the
/// ColdNet equivalent of CNMULTIPAGE. Built on <c>Magick.NET</c> (Apache-2.0, cross-platform).
/// </summary>
[ModuleDefinition("MultiPageTiff", ModuleCategory.GraphicsConversion, "Multi-page TIFF", "Combines single-page images into one multi-page TIFF.", OriginalModule = "CNMULTIPAGE", SettingsType = typeof(MultiPageTiffSettings), UsesFileExtension = false)]
public class MultiPageTiffModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<MultiPageTiffSettings>();
        var mask = settings.SourceFileMask.Replace("{prefix}", context.Job.FilePrefix);

        var files = Directory.EnumerateFiles(context.InputDirectory, mask)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"No source pages matched '{mask}' in {context.InputDirectory}"));
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using (var collection = new MagickImageCollection())
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = new MagickImage(file)
                {
                    Format = MagickFormat.Tiff
                };
                page.Settings.Compression = CompressionMethod.LZW;
                collection.Add(page);
            }

            collection.Write(outputPath, MagickFormat.Tiff);
        }

        if (context.Common.DeleteSourceFile)
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        return Task.FromResult(ModuleExecutionResult.Ok());
    }
}
