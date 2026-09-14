namespace ColdNet.Core.Modules;

/// <summary>
/// The module categories from the d.cold manual's module catalogue (Importmodule, Textkonvertierung,
/// XML, Tools, Job-Separation, Eigenschaftsermittlung, Hostkonvertierung, ERP, Grafikkonvertierung,
/// Komprimierung, Bedingte Zuweisung, Dateibehandlung). Kept as the same 12 categories so the
/// ColdNet module catalogue can be read side-by-side with the original documentation.
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
}
