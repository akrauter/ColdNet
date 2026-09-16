using ColdNet.Core.Modules;

namespace ColdNet.Modules.RemoteTransfer;

public class SftpExportSettings
{
    public RemoteTransferProtocol Protocol { get; set; } = RemoteTransferProtocol.Sftp;

    public string Host { get; set; } = string.Empty;

    /// <summary>0 = protocol default (22 for SFTP, 21 for FTPS/FTP).</summary>
    public int Port { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>SFTP only: path to a private key file, used instead of <see cref="Password"/> when set.</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    public string PrivateKeyPassphrase { get; set; } = string.Empty;

    public int ConnectTimeoutSeconds { get; set; } = 30;

    /// <summary>FTPS only: accept the server's TLS certificate even if it doesn't validate (e.g. self-signed). Off by default.</summary>
    public bool FtpsAcceptAnyCertificate { get; set; }

    public string RemoteDirectory { get; set; } = "/";

    /// <summary>Glob (relative to the input directory) selecting which of the job's files to upload, e.g. "{prefix}.*".</summary>
    public string SourceFileMask { get; set; } = "{prefix}.*";

    public bool CreateRemoteDirectoryIfMissing { get; set; } = true;

    public bool OverwriteExisting { get; set; } = true;
}

/// <summary>
/// Uploads a job's file(s) to a remote SFTP/FTPS/FTP server - a network-attached counterpart to
/// <c>FileMover</c>. Deletes the local source file(s) after a successful upload when the module's
/// common "Delete source file" setting is enabled, same as every other module.
/// </summary>
[ModuleDefinition("SftpExport", ModuleCategory.RemoteTransfer, "SFTP/FTPS Export", "Uploads a job's files to a remote SFTP/FTPS/FTP server.", SettingsType = typeof(SftpExportSettings))]
public class SftpExportModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SftpExportSettings>();

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            return ModuleExecutionResult.Fail("SftpExport requires a Host to connect to.");
        }

        var mask = settings.SourceFileMask.Replace("{prefix}", context.Job.FilePrefix);
        var files = Directory.EnumerateFiles(context.InputDirectory, mask).ToList();

        if (files.Count == 0)
        {
            return ModuleExecutionResult.Fail($"No files matched '{mask}' in {context.InputDirectory}");
        }

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
            return ModuleExecutionResult.Fail($"Could not connect to {settings.Host}:{connectionOptions.Port}: {ex.Message}");
        }

        if (settings.CreateRemoteDirectoryIfMissing)
        {
            await client.EnsureDirectoryAsync(settings.RemoteDirectory, cancellationToken);
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remotePath = RemotePath.Combine(settings.RemoteDirectory, Path.GetFileName(file));

            try
            {
                await client.UploadFileAsync(file, remotePath, settings.OverwriteExisting, cancellationToken);
            }
            catch (Exception ex)
            {
                return ModuleExecutionResult.Fail($"Failed to upload {Path.GetFileName(file)} to {settings.Host}:{remotePath}: {ex.Message}");
            }
        }

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
