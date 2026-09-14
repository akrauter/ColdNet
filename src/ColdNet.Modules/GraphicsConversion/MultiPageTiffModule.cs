using System.Drawing;
using System.Runtime.Versioning;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.GraphicsConversion;

public class MultiPageTiffSettings
{
    /// <summary>Glob (relative to the input directory) matching the single-page source files, e.g. "{prefix}_*.tif".</summary>
    public string SourceFileMask { get; set; } = "{prefix}_*.tif";
}

/// <summary>
/// Combines several single-page images sharing the job's prefix into one multi-page TIFF - the
/// ColdNet equivalent of DCMULTIPAGE. Built on <c>System.Drawing.Common</c> (MIT, Windows-only)
/// instead of a third-party imaging library to keep this module's dependencies MIT-licensed.
/// </summary>
[ModuleDefinition("MultiPageTiff", ModuleCategory.GraphicsConversion, "Multi-page TIFF", "Combines single-page images into one multi-page TIFF.", OriginalModule = "DCMULTIPAGE", SettingsType = typeof(MultiPageTiffSettings))]
[SupportedOSPlatform("windows")]
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

        var pages = new List<Bitmap>(files.Count);
        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(new Bitmap(file));
            }

            GdiTiff.WriteFrames(outputPath, pages);
        }
        finally
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }
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
