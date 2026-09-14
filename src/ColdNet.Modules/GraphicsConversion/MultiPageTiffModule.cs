using ColdNet.Core.Modules;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace ColdNet.Modules.GraphicsConversion;

public class MultiPageTiffSettings
{
    /// <summary>Glob (relative to the input directory) matching the single-page source files, e.g. "{prefix}_*.tif".</summary>
    public string SourceFileMask { get; set; } = "{prefix}_*.tif";
}

/// <summary>
/// Combines several single-page images sharing the job's prefix into one multi-page TIFF - the
/// ColdNet equivalent of DCMULTIPAGE.
/// </summary>
[ModuleDefinition("MultiPageTiff", ModuleCategory.GraphicsConversion, "Multi-page TIFF", "Combines single-page images into one multi-page TIFF.", OriginalModule = "DCMULTIPAGE", SettingsType = typeof(MultiPageTiffSettings))]
public class MultiPageTiffModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<MultiPageTiffSettings>();
        var mask = settings.SourceFileMask.Replace("{prefix}", context.Job.FilePrefix);

        var files = Directory.EnumerateFiles(context.InputDirectory, mask)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            return ModuleExecutionResult.Fail($"No source pages matched '{mask}' in {context.InputDirectory}");
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var target = await Image.LoadAsync<Rgba32>(files[0], cancellationToken);

        for (var i = 1; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = await Image.LoadAsync<Rgba32>(files[i], cancellationToken);
            target.Frames.AddFrame(page.Frames.RootFrame);
        }

        await target.SaveAsTiffAsync(outputPath, new TiffEncoder(), cancellationToken);

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
