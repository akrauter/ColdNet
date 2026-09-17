using System.Text.Json;
using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging;

namespace ColdNet.Modules.RemoteTransfer;

public enum RemoteImportPostAction
{
    /// <summary>Leave the file on the remote server (relies entirely on local de-duplication - see <see cref="SftpImportModule"/>).</summary>
    None,

    /// <summary>Rename the remote file (append <see cref="SftpImportSettings.RenameSuffix"/>) once it has been downloaded.</summary>
    Rename,

    /// <summary>Delete the remote file once it has been downloaded.</summary>
    Delete,
}

public class SftpImportSettings
{
    public RemoteTransferProtocol Protocol { get; set; } = RemoteTransferProtocol.Sftp;

    public string Host { get; set; } = string.Empty;

    /// <summary>0 = protocol default (22 for SFTP, 21 for FTPS/FTP).</summary>
    public int Port { get; set; }

    public string UserName { get; set; } = string.Empty;

    [SensitiveValue]
    public string Password { get; set; } = string.Empty;

    /// <summary>SFTP only: path to a private key file, used instead of <see cref="Password"/> when set.</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    [SensitiveValue]
    public string PrivateKeyPassphrase { get; set; } = string.Empty;

    public int ConnectTimeoutSeconds { get; set; } = 30;

    /// <summary>FTPS only: accept the server's TLS certificate even if it doesn't validate (e.g. self-signed). Off by default.</summary>
    public bool FtpsAcceptAnyCertificate { get; set; }

    public string RemoteDirectory { get; set; } = "/";

    /// <summary>Simple "*"/"?" glob, e.g. "*.pdf" - not a regex.</summary>
    public string FileMask { get; set; } = "*.*";

    public bool GenerateUniqueJobId { get; set; } = true;

    public int MaxFilesPerPoll { get; set; } = 20;

    public RemoteImportPostAction PostDownloadAction { get; set; } = RemoteImportPostAction.Rename;

    /// <summary>Used when <see cref="PostDownloadAction"/> is Rename.</summary>
    public string RenameSuffix { get; set; } = ".imported";
}

/// <summary>
/// The remote counterpart of <c>ColdImport</c>: connects to an SFTP/FTPS/FTP server, lists
/// <see cref="SftpImportSettings.RemoteDirectory"/>, and downloads every new matching file into
/// <see cref="CommonModuleSettings.Directory"/> as its own job - exactly like CNIMPORT, except the
/// source directory lives on a remote server instead of local disk. Because remote servers have no
/// equivalent of the local "$"-prefix rename trick, re-processing is prevented two ways: the
/// configured <see cref="RemoteImportPostAction"/> (rename/delete the remote file), and - since
/// that can silently fail against a read-only account, confirmed by testing this module against a
/// real public SFTP server - a local record of already-downloaded remote paths
/// (<c>.sftp-import-state.json</c> next to the downloaded jobs) that's always consulted regardless
/// of whether the remote-side action succeeded.
/// </summary>
[ModuleDefinition("SftpImport", ModuleCategory.Import, "SFTP/FTPS Import", "Pulls new files from a remote SFTP/FTPS/FTP server and creates a job for each.", SettingsType = typeof(SftpImportSettings), UsesFileExtension = false, UsesOutputFileExtension = false)]
public class SftpImportModule : IJobImportModule
{
    private const string StateFileName = ".sftp-import-state.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<NewJobRequest>> DiscoverJobsAsync(
        ProcessChain chain,
        ModuleInstance moduleInstance,
        ILogger logger,
        ISecretProtector secretProtector,
        ISet<string> existingFilePrefixes,
        CancellationToken cancellationToken)
    {
        var common = moduleInstance.CommonSettings;
        var settings = LoadSettings(moduleInstance, secretProtector);

        var results = new List<NewJobRequest>();

        if (string.IsNullOrWhiteSpace(common.Directory))
        {
            logger.LogError("SftpImport requires a local Directory to download files into.");
            return results;
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            logger.LogError("SftpImport requires a Host to connect to.");
            return results;
        }

        Directory.CreateDirectory(common.Directory);

        var connectionOptions = RemoteConnectionOptions.Resolve(
            settings.Protocol, settings.Host, settings.Port, settings.UserName, settings.Password,
            settings.PrivateKeyPath, settings.PrivateKeyPassphrase, settings.ConnectTimeoutSeconds,
            settings.FtpsAcceptAnyCertificate);

        await using var client = RemoteTransferClientFactory.Create(connectionOptions);

        try
        {
            await client.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SftpImport could not connect to {Host}:{Port}", settings.Host, connectionOptions.Port);
            return results;
        }

        var stateFilePath = Path.Combine(common.Directory, StateFileName);
        var seenRemotePaths = await LoadSeenPathsAsync(stateFilePath, cancellationToken);
        var newlySeen = false;

        var files = await client.ListFilesAsync(settings.RemoteDirectory, cancellationToken);
        var matching = files
            .Where(f => WildcardMatcher.IsMatch(f.Name, settings.FileMask))
            .Where(f => !seenRemotePaths.Contains(RemotePath.Combine(settings.RemoteDirectory, f.Name)))
            .Take(Math.Max(1, settings.MaxFilesPerPoll));

        foreach (var file in matching)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remotePath = RemotePath.Combine(settings.RemoteDirectory, file.Name);
            var jobNumber = settings.GenerateUniqueJobId
                ? JobNumberGenerator.GenerateUniqueJobId()
                : Path.GetFileNameWithoutExtension(file.Name);
            jobNumber = JobNumberGenerator.MakeUnique(jobNumber, existingFilePrefixes);
            var localPath = Path.Combine(common.Directory, jobNumber + Path.GetExtension(file.Name));

            try
            {
                await client.DownloadFileAsync(remotePath, localPath, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SftpImport failed to download {RemotePath} from {Host}", remotePath, settings.Host);
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                continue;
            }

            try
            {
                switch (settings.PostDownloadAction)
                {
                    case RemoteImportPostAction.Delete:
                        await client.DeleteFileAsync(remotePath, cancellationToken);
                        break;
                    case RemoteImportPostAction.Rename:
                        await client.RenameFileAsync(remotePath, remotePath + settings.RenameSuffix, cancellationToken);
                        break;
                    case RemoteImportPostAction.None:
                        break;
                }
            }
            catch (Exception ex)
            {
                // Local de-duplication below covers us either way, so this is a soft warning, not
                // a failure - a read-only service account is a normal, supported configuration.
                logger.LogWarning(ex, "Downloaded {RemotePath} but could not apply the post-download action on the server (e.g. a read-only account) - local de-duplication will still prevent re-import", remotePath);
            }

            seenRemotePaths.Add(remotePath);
            newlySeen = true;

            results.Add(new NewJobRequest(jobNumber, common.Directory));
            logger.LogInformation("Imported job {JobNumber} from {Host}:{RemotePath}", jobNumber, settings.Host, remotePath);
        }

        if (newlySeen)
        {
            await SaveSeenPathsAsync(stateFilePath, seenRemotePaths, cancellationToken);
        }

        return results;
    }

    internal static SftpImportSettings LoadSettings(ModuleInstance moduleInstance, ISecretProtector secretProtector) =>
        string.IsNullOrWhiteSpace(moduleInstance.SettingsJson) || moduleInstance.SettingsJson == "{}"
            ? new SftpImportSettings()
            : JsonSerializer.Deserialize<SftpImportSettings>(
                SettingsEncryption.Decrypt(moduleInstance.SettingsJson, typeof(SftpImportSettings), secretProtector),
                JsonOptions) ?? new SftpImportSettings();

    private static async Task<HashSet<string>> LoadSeenPathsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(path);
        var paths = await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: cancellationToken);
        return paths is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(paths, StringComparer.Ordinal);
    }

    private static async Task SaveSeenPathsAsync(string path, HashSet<string> paths, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, paths, cancellationToken: cancellationToken);
    }
}
