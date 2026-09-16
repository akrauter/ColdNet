namespace ColdNet.Modules.RemoteTransfer;

public enum RemoteTransferProtocol
{
    /// <summary>SSH File Transfer Protocol - recommended default.</summary>
    Sftp,

    /// <summary>FTP over explicit TLS.</summary>
    Ftps,

    /// <summary>Plain, unencrypted FTP - kept for legacy servers; prefer Sftp or Ftps.</summary>
    Ftp,
}
