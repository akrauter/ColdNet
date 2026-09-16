using FluentFTP;

namespace ColdNet.Modules.RemoteTransfer;

/// <summary>FTPS (explicit TLS) / plain FTP client backed by FluentFTP.</summary>
internal sealed class FtpTransferClient(RemoteConnectionOptions options) : IRemoteFileTransferClient
{
    private AsyncFtpClient? _client;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new AsyncFtpClient(options.Host, options.UserName, options.Password, options.Port);
        client.Config.ConnectTimeout = (int)TimeSpan.FromSeconds(options.ConnectTimeoutSeconds).TotalMilliseconds;

        if (options.Protocol == RemoteTransferProtocol.Ftps)
        {
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
            // Off by default: only bypass certificate validation when the module is explicitly
            // configured to (e.g. an internal server with a self-signed certificate).
            client.Config.ValidateAnyCertificate = options.FtpsAcceptAnyCertificate;
        }

        await client.Connect(cancellationToken);
        _client = client;
    }

    public async Task<IReadOnlyList<RemoteFileEntry>> ListFilesAsync(string remoteDirectory, CancellationToken cancellationToken)
    {
        var listing = await Client.GetListing(remoteDirectory, cancellationToken);
        return listing
            .Where(i => i.Type == FtpObjectType.File)
            .Select(i => new RemoteFileEntry(i.Name, i.Size))
            .ToList();
    }

    public Task DownloadFileAsync(string remotePath, string localPath, CancellationToken cancellationToken) =>
        Client.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite, token: cancellationToken);

    public Task UploadFileAsync(string localPath, string remotePath, bool overwrite, CancellationToken cancellationToken) =>
        Client.UploadFile(localPath, remotePath, overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip, token: cancellationToken);

    public Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken) =>
        Client.DeleteFile(remotePath, cancellationToken);

    public Task RenameFileAsync(string oldRemotePath, string newRemotePath, CancellationToken cancellationToken) =>
        Client.Rename(oldRemotePath, newRemotePath, cancellationToken);

    public Task EnsureDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken) =>
        Client.CreateDirectory(remoteDirectory, cancellationToken);

    private AsyncFtpClient Client => _client ?? throw new InvalidOperationException("FTP client is not connected. Call ConnectAsync first.");

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            if (_client.IsConnected)
            {
                await _client.Disconnect();
            }

            _client.Dispose();
        }
    }
}
