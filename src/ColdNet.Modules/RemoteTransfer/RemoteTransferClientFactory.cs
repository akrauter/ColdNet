namespace ColdNet.Modules.RemoteTransfer;

internal static class RemoteTransferClientFactory
{
    public static IRemoteFileTransferClient Create(RemoteConnectionOptions options) => options.Protocol switch
    {
        RemoteTransferProtocol.Sftp => new SftpTransferClient(options),
        RemoteTransferProtocol.Ftps or RemoteTransferProtocol.Ftp => new FtpTransferClient(options),
        _ => throw new NotSupportedException($"Unsupported remote transfer protocol: {options.Protocol}"),
    };
}
