using Renci.SshNet;

namespace ColdNet.Modules.RemoteTransfer;

/// <summary>SFTP (SSH File Transfer Protocol) client backed by SSH.NET. SSH.NET's API is synchronous, so every call is dispatched via <see cref="Task.Run(Action)"/>.</summary>
internal sealed class SftpTransferClient(RemoteConnectionOptions options) : IRemoteFileTransferClient
{
    private SftpClient? _client;

    public Task ConnectAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        AuthenticationMethod auth = !string.IsNullOrWhiteSpace(options.PrivateKeyPath)
            ? new PrivateKeyAuthenticationMethod(options.UserName, new PrivateKeyFile(
                options.PrivateKeyPath,
                string.IsNullOrEmpty(options.PrivateKeyPassphrase) ? null : options.PrivateKeyPassphrase))
            : new PasswordAuthenticationMethod(options.UserName, options.Password);

        var connectionInfo = new ConnectionInfo(options.Host, options.Port, options.UserName, auth)
        {
            Timeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds),
        };

        _client = new SftpClient(connectionInfo);
        _client.Connect();
    }, cancellationToken);

    public Task<IReadOnlyList<RemoteFileEntry>> ListFilesAsync(string remoteDirectory, CancellationToken cancellationToken) => Task.Run(() =>
    {
        IReadOnlyList<RemoteFileEntry> entries = Client.ListDirectory(remoteDirectory)
            .Where(f => f.IsRegularFile)
            .Select(f => new RemoteFileEntry(f.Name, f.Length))
            .ToList();
        return entries;
    }, cancellationToken);

    public Task DownloadFileAsync(string remotePath, string localPath, CancellationToken cancellationToken) => Task.Run(() =>
    {
        using var localStream = File.Create(localPath);
        Client.DownloadFile(remotePath, localStream);
    }, cancellationToken);

    public Task UploadFileAsync(string localPath, string remotePath, bool overwrite, CancellationToken cancellationToken) => Task.Run(() =>
    {
        using var localStream = File.OpenRead(localPath);
        Client.UploadFile(localStream, remotePath, overwrite);
    }, cancellationToken);

    public Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken) => Task.Run(() =>
        Client.DeleteFile(remotePath), cancellationToken);

    public Task RenameFileAsync(string oldRemotePath, string newRemotePath, CancellationToken cancellationToken) => Task.Run(() =>
        Client.RenameFile(oldRemotePath, newRemotePath), cancellationToken);

    public Task EnsureDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken) => Task.Run(() =>
    {
        if (!Client.Exists(remoteDirectory))
        {
            Client.CreateDirectory(remoteDirectory);
        }
    }, cancellationToken);

    private SftpClient Client => _client ?? throw new InvalidOperationException("SFTP client is not connected. Call ConnectAsync first.");

    public ValueTask DisposeAsync()
    {
        if (_client is { IsConnected: true })
        {
            _client.Disconnect();
        }

        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
