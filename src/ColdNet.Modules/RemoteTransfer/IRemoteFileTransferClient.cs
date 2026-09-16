namespace ColdNet.Modules.RemoteTransfer;

internal sealed record RemoteFileEntry(string Name, long SizeBytes);

/// <summary>
/// A connected remote file transfer session. One protocol-specific implementation per
/// <see cref="RemoteTransferProtocol"/> (<see cref="SftpTransferClient"/> for Sftp,
/// <see cref="FtpTransferClient"/> for Ftps/Ftp) - created fresh per module invocation via
/// <see cref="RemoteTransferClientFactory"/> and disposed (closing the connection) right after.
/// </summary>
internal interface IRemoteFileTransferClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteFileEntry>> ListFilesAsync(string remoteDirectory, CancellationToken cancellationToken);

    Task DownloadFileAsync(string remotePath, string localPath, CancellationToken cancellationToken);

    Task UploadFileAsync(string localPath, string remotePath, bool overwrite, CancellationToken cancellationToken);

    Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken);

    Task RenameFileAsync(string oldRemotePath, string newRemotePath, CancellationToken cancellationToken);

    Task EnsureDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken);
}
