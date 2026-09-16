namespace ColdNet.Modules.RemoteTransfer;

/// <summary>
/// Resolved connection parameters for one remote transfer session - built from whichever settings
/// class (<see cref="SftpImportSettings"/> or <see cref="SftpExportSettings"/>) triggered it, so
/// <see cref="IRemoteFileTransferClient"/> implementations don't need to know about either.
/// </summary>
internal sealed record RemoteConnectionOptions(
    RemoteTransferProtocol Protocol,
    string Host,
    int Port,
    string UserName,
    string Password,
    string? PrivateKeyPath,
    string? PrivateKeyPassphrase,
    int ConnectTimeoutSeconds,
    bool FtpsAcceptAnyCertificate)
{
    public static RemoteConnectionOptions Resolve(
        RemoteTransferProtocol protocol,
        string host,
        int port,
        string userName,
        string password,
        string? privateKeyPath,
        string? privateKeyPassphrase,
        int connectTimeoutSeconds,
        bool ftpsAcceptAnyCertificate)
    {
        var resolvedPort = port > 0 ? port : protocol == RemoteTransferProtocol.Sftp ? 22 : 21;
        return new RemoteConnectionOptions(
            protocol, host, resolvedPort, userName, password,
            privateKeyPath, privateKeyPassphrase,
            connectTimeoutSeconds > 0 ? connectTimeoutSeconds : 30,
            ftpsAcceptAnyCertificate);
    }
}
