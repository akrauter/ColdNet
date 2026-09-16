using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

namespace ColdNet.Modules.Import;

public class ColdImportSettings
{
    /// <summary>File mask, e.g. "*.pdf" - not just an extension, mirrors CNIMPORT's "File mask" field.</summary>
    public string FileMask { get; set; } = "*.*";

    /// <summary>Generates a unique 12-character job number instead of using the source file name.</summary>
    public bool GenerateUniqueJobId { get; set; } = true;

    /// <summary>Also imports files from sub-directories of the configured Directory.</summary>
    public bool SearchSubdirectories { get; set; }

    /// <summary>Skips (and marks as .err) zero-byte files instead of creating a job for them.</summary>
    public bool IgnoreZeroByteFiles { get; set; } = true;
}

/// <summary>
/// The mandatory first module of every chain - the ColdNet equivalent of CNIMPORT. Scans
/// <see cref="CommonModuleSettings.Directory"/> for files matching <see cref="ColdImportSettings.FileMask"/>,
/// groups every file sharing a name prefix into one job, renames them to the job number with a
/// leading "$" on the extension (e.g. Test.pdf -&gt; 00106EL123317.$pdf) so the same delivery is
/// never imported twice, and reports the new jobs to the engine.
/// </summary>
[ModuleDefinition("ColdImport", ModuleCategory.Import, "Import", "Watches a directory and creates a job for every incoming file (group).", OriginalModule = "CNIMPORT", SettingsType = typeof(ColdImportSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class ColdImportModule : IJobImportModule
{
    public Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        ISecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        var common = moduleInstance.CommonSettings;
        var settings = string.IsNullOrWhiteSpace(moduleInstance.SettingsJson) || moduleInstance.SettingsJson == "{}"
            ? new ColdImportSettings()
            : System.Text.Json.JsonSerializer.Deserialize<ColdImportSettings>(moduleInstance.SettingsJson) ?? new ColdImportSettings();

        var results = new List<NewJobRequest>();

        if (string.IsNullOrWhiteSpace(common.Directory) || !Directory.Exists(common.Directory))
        {
            return Task.FromResult<IReadOnlyList<NewJobRequest>>(results);
        }

        var searchOption = settings.SearchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var processedPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var triggerFile in Directory.EnumerateFiles(common.Directory, settings.FileMask, searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = Path.GetDirectoryName(triggerFile)!;
            var prefix = Path.GetFileNameWithoutExtension(triggerFile);
            var groupKey = directory + "|" + prefix;

            if (!processedPrefixes.Add(groupKey))
            {
                continue; // already handled as part of an earlier trigger file's group
            }

            var siblingFiles = Directory.EnumerateFiles(directory, prefix + ".*")
                .Where(f => !Path.GetExtension(f).StartsWith(".$", StringComparison.Ordinal))
                .ToList();

            if (siblingFiles.Count == 0)
            {
                continue;
            }

            if (!TryLockAll(siblingFiles))
            {
                logger.LogDebug("Skipping job {Prefix}: at least one file is still being written to", prefix);
                continue;
            }

            if (settings.IgnoreZeroByteFiles && siblingFiles.Any(f => new FileInfo(f).Length == 0))
            {
                foreach (var f in siblingFiles.Where(f => new FileInfo(f).Length == 0))
                {
                    File.Move(f, Path.ChangeExtension(f, ".err"), overwrite: true);
                }

                logger.LogWarning("Job {Prefix} contained a 0-byte file and was marked .err", prefix);
                continue;
            }

            if (common.Save)
            {
                BackupFiles(directory, siblingFiles);
            }

            var jobNumber = settings.GenerateUniqueJobId ? JobNumberGenerator.GenerateUniqueJobId() : prefix;

            foreach (var file in siblingFiles)
            {
                var ext = Path.GetExtension(file); // includes leading dot
                var newName = jobNumber + ".$" + ext.TrimStart('.');
                var newPath = Path.Combine(directory, newName);
                File.Move(file, newPath, overwrite: false);
            }

            results.Add(new NewJobRequest(jobNumber, directory));
            logger.LogInformation("Imported job {JobNumber} ({FileCount} file(s)) from {Directory}", jobNumber, siblingFiles.Count, directory);
        }

        return Task.FromResult<IReadOnlyList<NewJobRequest>>(results);
    }

    private static bool TryLockAll(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            try
            {
                using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                return false;
            }
        }

        return true;
    }

    private static void BackupFiles(string directory, IEnumerable<string> files)
    {
        var saveDir = Path.Combine(directory, "SAVE", DateTime.UtcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(saveDir);

        foreach (var file in files)
        {
            var target = Path.Combine(saveDir, Path.GetFileName(file));
            var name = Path.GetFileNameWithoutExtension(target);
            var ext = Path.GetExtension(target);
            var dir = Path.GetDirectoryName(target)!;

            for (var i = 1; File.Exists(target); i++)
            {
                target = Path.Combine(dir, $"{name}.{i}{ext}");
            }

            File.Copy(file, target);
        }
    }
}
