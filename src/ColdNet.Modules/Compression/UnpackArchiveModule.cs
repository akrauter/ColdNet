using System.IO.Compression;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.Compression;

public class UnpackArchiveSettings
{
    public bool OverwriteExisting { get; set; } = true;
}

/// <summary>
/// Extracts a ZIP archive belonging to the job into the output directory - the ColdNet
/// equivalent of CNUNPACK.
/// </summary>
[ModuleDefinition("UnpackArchive", ModuleCategory.Compression, "Unpack Archive", "Extracts a ZIP archive into the output directory.", OriginalModule = "CNUNPACK", SettingsType = typeof(UnpackArchiveSettings), UsesOutputFileExtension = false)]
public class UnpackArchiveModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<UnpackArchiveSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Archive not found: {inputPath}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        var targetDir = context.OutputDirectory;
        Directory.CreateDirectory(targetDir);

        using (var archive = ZipFile.OpenRead(inputPath))
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // directory entry
                }

                var destination = Path.Combine(targetDir, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, settings.OverwriteExisting);
            }
        }

        if (context.Common.DeleteSourceFile)
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }
}
