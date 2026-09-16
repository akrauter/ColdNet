namespace ColdNet.Core.Modules;

/// <summary>
/// The module catalogue's categories (Importmodule, Textkonvertierung, XML, Tools, Job-Separation,
/// Eigenschaftsermittlung, Hostkonvertierung, ERP, Grafikkonvertierung, Komprimierung, Bedingte
/// Zuweisung, Dateibehandlung).
/// </summary>
public enum ModuleCategory
{
    Import,
    TextConversion,
    Xml,
    Tools,
    JobSeparation,
    PropertyExtraction,
    Hosts,
    Erp,
    GraphicsConversion,
    Compression,
    Case,
    FileHandling,
    DmsExport,

    /// <summary>
    /// ColdNet-specific category for modules that move files to/from a remote server over a
    /// network file transfer protocol - e.g. SFTP/FTPS/FTP.
    /// </summary>
    RemoteTransfer,
}
