using System.Text.Json;
using ColdNet.Core.Domain;
using ColdNet.Core.Properties;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

namespace ColdNet.Core.Modules;

/// <summary>
/// Everything a module needs to process one job: the job itself, its chain/instance configuration,
/// a logger, and a handful of path/backup/property helpers that follow the conventions every
/// ColdNet module observes (General tab paths, the Save backup folder, the job's property file).
/// </summary>
public sealed class ModuleExecutionContext(
    Job job,
    ProcessChain chain,
    ModuleInstance moduleInstance,
    ILogger logger,
    ISecretProtector? secretProtector = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISecretProtector _secretProtector = secretProtector ?? NullSecretProtector.Instance;

    public Job Job { get; } = job;

    public ProcessChain Chain { get; } = chain;

    public ModuleInstance ModuleInstance { get; } = moduleInstance;

    public ILogger Logger { get; } = logger;

    public CommonModuleSettings Common => ModuleInstance.CommonSettings;

    public DmsSupportSettings DmsSupport => ModuleInstance.DmsSupport;

    /// <summary>
    /// Deserializes the module instance's module-specific settings JSON into <typeparamref name="T"/>,
    /// transparently decrypting any <see cref="SensitiveValueAttribute"/>-marked field (passwords,
    /// passphrases, ...) back to plaintext first.
    /// </summary>
    public T GetSettings<T>() where T : new()
    {
        if (string.IsNullOrWhiteSpace(ModuleInstance.SettingsJson) || ModuleInstance.SettingsJson == "{}")
        {
            return new T();
        }

        var decrypted = SettingsEncryption.Decrypt(ModuleInstance.SettingsJson, typeof(T), _secretProtector);
        return JsonSerializer.Deserialize<T>(decrypted, JsonOptions) ?? new T();
    }

    public string InputDirectory =>
        string.IsNullOrWhiteSpace(Common.Directory) ? Job.WorkDirectory : Common.Directory;

    public string OutputDirectory =>
        string.IsNullOrWhiteSpace(Common.OutputDirectory) ? InputDirectory : Common.OutputDirectory!;

    /// <summary>Resolves the job's input file path using the module's configured directory/extension.</summary>
    public string GetInputPath(string? extensionOverride = null)
    {
        var ext = extensionOverride ?? Common.FileExtension;
        return Path.Combine(InputDirectory, Job.FilePrefix + NormalizeExtension(ext));
    }

    /// <summary>Resolves the job's output file path using the module's configured output directory/extension.</summary>
    public string GetOutputPath(string? extensionOverride = null)
    {
        var ext = extensionOverride ?? Common.OutputFileExtension ?? Common.FileExtension;
        return Path.Combine(OutputDirectory, Job.FilePrefix + NormalizeExtension(ext));
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith('.') ? extension : "." + extension;
    }

    /// <summary>
    /// The job's ambient property file - always <c>&lt;prefix&gt;.properties.json</c> next to the
    /// job's files, independent of whatever directory/extension the current module is configured
    /// for. This is what property-extraction modules (CNSETVAR/CNPARSE-style) read and write, and
    /// what later modules (e.g. the EDMVault export) consume to build the DMS index file.
    /// </summary>
    public string PropertiesFilePath => Path.Combine(Job.WorkDirectory, Job.FilePrefix + ".properties.json");

    public Task<PropertyBag> LoadPropertiesAsync(CancellationToken ct = default) =>
        PropertyBag.LoadAsync(PropertiesFilePath, ct);

    public Task SavePropertiesAsync(PropertyBag bag, CancellationToken ct = default) =>
        bag.SaveAsync(PropertiesFilePath, ct);

    /// <summary>
    /// If <see cref="CommonModuleSettings.Save"/> is set, copies every file sharing the job's
    /// prefix into <c>&lt;InputDirectory&gt;/SAVE/&lt;yyyyMMdd&gt;/</c> before processing touches
    /// them, so the original state can always be recovered.
    /// </summary>
    public async Task BackupSourceFilesAsync(CancellationToken ct = default)
    {
        if (!Common.Save || !System.IO.Directory.Exists(InputDirectory))
        {
            return;
        }

        var saveDir = Path.Combine(InputDirectory, "SAVE", DateTime.UtcNow.ToString("yyyyMMdd"));
        System.IO.Directory.CreateDirectory(saveDir);

        foreach (var file in System.IO.Directory.EnumerateFiles(InputDirectory, Job.FilePrefix + ".*"))
        {
            ct.ThrowIfCancellationRequested();
            var target = MakeUniqueTarget(Path.Combine(saveDir, Path.GetFileName(file)));
            File.Copy(file, target);
        }
    }

    private static string MakeUniqueTarget(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name}.{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
