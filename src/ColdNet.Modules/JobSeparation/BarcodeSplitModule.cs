using System.Drawing;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ColdNet.Core.Modules;
using ColdNet.Modules.GraphicsConversion;
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
/// the same chain as its own job, starting at the module right after this one. Page rendering is
/// built on <c>System.Drawing.Common</c> (MIT, Windows-only); barcode decoding on ZXing.Net
/// (Apache-2.0).
/// </summary>
[ModuleDefinition("BarcodeSplit", ModuleCategory.JobSeparation, "Barcode Split", "Splits a multi-page TIFF into separate jobs at barcode separator pages.", OriginalModule = "DCBARCODE", SettingsType = typeof(BarcodeSplitSettings))]
[SupportedOSPlatform("windows")]
public class BarcodeSplitModule : IColdModule
{
    public Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<BarcodeSplitSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return Task.FromResult(ModuleExecutionResult.Fail($"Input file not found: {inputPath}"));
        }

        var frames = GdiTiff.ReadFrames(inputPath);
        try
        {
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = { TryHarder = true, PossibleFormats = null },
            };
            var separatorRegex = new Regex(settings.SeparatorPattern);

            var documents = new List<List<int>>();
            var current = new List<int>();

            for (var i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isSeparator = TryDecodeSeparator(reader, frames[i], separatorRegex);

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
                return Task.FromResult(ModuleExecutionResult.Ok());
            }

            var outputExtension = context.Common.OutputFileExtension ?? context.Common.FileExtension;
            var spawned = new List<NewJobRequest>();
            Directory.CreateDirectory(context.OutputDirectory);

            for (var d = 0; d < documents.Count; d++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageIndexes = documents[d];
                var newPrefix = $"{context.Job.FilePrefix}-{d + 1:000}";
                var outputPath = Path.Combine(context.OutputDirectory, newPrefix + ModuleExecutionContext.NormalizeExtension(outputExtension));

                var pagesForDoc = pageIndexes.Select(idx => frames[idx]).ToList();
                GdiTiff.WriteFrames(outputPath, pagesForDoc);

                spawned.Add(new NewJobRequest(newPrefix, context.OutputDirectory));
            }

            if (context.Common.DeleteSourceFile)
            {
                File.Delete(inputPath);
            }

            return Task.FromResult(ModuleExecutionResult.Ok(spawnedJobs: spawned, finishJob: true));
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }
    }

    private static bool TryDecodeSeparator(BarcodeReaderGeneric reader, Bitmap frame, Regex separatorRegex)
    {
        var buffer = GdiTiff.GetBgra32Bytes(frame);
        var luminanceSource = new RGBLuminanceSource(buffer, frame.Width, frame.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var result = reader.Decode(luminanceSource);
        return result is not null && separatorRegex.IsMatch(result.Text);
    }
}
