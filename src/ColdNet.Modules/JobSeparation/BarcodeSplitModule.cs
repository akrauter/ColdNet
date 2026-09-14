using System.Text.RegularExpressions;
using ColdNet.Core.Modules;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;

namespace ColdNet.Modules.JobSeparation;

public class BarcodeSplitSettings
{
    /// <summary>Regex a decoded barcode's text must match to be treated as a document separator page.</summary>
    public string SeparatorPattern { get; set; } = "^JOBSEP";

    /// <summary>Drops the separator page itself from the resulting documents.</summary>
    public bool RemoveSeparatorPage { get; set; } = true;
}

/// <summary>
/// Splits a multi-page TIFF into several new jobs wherever a page carries a barcode matching
/// <see cref="BarcodeSplitSettings.SeparatorPattern"/> - the ColdNet equivalent of DCBARCODE /
/// DCJOBSEP. The triggering job is finished once split; each resulting document continues down
/// the same chain as its own job, starting at the module right after this one.
/// </summary>
[ModuleDefinition("BarcodeSplit", ModuleCategory.JobSeparation, "Barcode Split", "Splits a multi-page TIFF into separate jobs at barcode separator pages.", OriginalModule = "DCBARCODE", SettingsType = typeof(BarcodeSplitSettings))]
public class BarcodeSplitModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<BarcodeSplitSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        using var source = await Image.LoadAsync<Rgba32>(inputPath, cancellationToken);
        var frameCount = source.Frames.Count;

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = { TryHarder = true, PossibleFormats = null },
        };
        var separatorRegex = new Regex(settings.SeparatorPattern);

        var documents = new List<List<int>>();
        var current = new List<int>();

        for (var i = 0; i < frameCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var frame = source.Frames.CloneFrame(i);
            var isSeparator = TryDecodeSeparator(reader, frame, separatorRegex);

            if (isSeparator)
            {
                if (current.Count > 0)
                {
                    documents.Add(current);
                    current = [];
                }

                if (!settings.RemoveSeparatorPage)
                {
                    current.Add(i);
                }
            }
            else
            {
                current.Add(i);
            }
        }

        if (current.Count > 0)
        {
            documents.Add(current);
        }

        if (documents.Count <= 1)
        {
            // No separator found (or only one document) - nothing to split, job continues untouched.
            return ModuleExecutionResult.Ok();
        }

        var outputExtension = context.Common.OutputFileExtension ?? context.Common.FileExtension;
        var spawned = new List<NewJobRequest>();

        for (var d = 0; d < documents.Count; d++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageIndexes = documents[d];
            var newPrefix = $"{context.Job.FilePrefix}-{d + 1:000}";
            var outputPath = Path.Combine(context.OutputDirectory, newPrefix + ModuleExecutionContext.NormalizeExtension(outputExtension));
            Directory.CreateDirectory(context.OutputDirectory);

            using var target = source.Frames.CloneFrame(pageIndexes[0]);
            for (var p = 1; p < pageIndexes.Count; p++)
            {
                using var page = source.Frames.CloneFrame(pageIndexes[p]);
                target.Frames.AddFrame(page.Frames.RootFrame);
            }

            await target.SaveAsTiffAsync(outputPath, new TiffEncoder(), cancellationToken);
            spawned.Add(new NewJobRequest(newPrefix, context.OutputDirectory));
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok(spawnedJobs: spawned, finishJob: true);
    }

    private static bool TryDecodeSeparator(BarcodeReaderGeneric reader, Image<Rgba32> frame, Regex separatorRegex)
    {
        var buffer = new byte[frame.Width * frame.Height * 4];
        frame.CopyPixelDataTo(buffer);

        var luminanceSource = new RGBLuminanceSource(buffer, frame.Width, frame.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var result = reader.Decode(luminanceSource);
        return result is not null && separatorRegex.IsMatch(result.Text);
    }
}
