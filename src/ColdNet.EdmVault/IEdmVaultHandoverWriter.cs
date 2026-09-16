using ColdNet.Core.Properties;

namespace ColdNet.EdmVault;

/// <summary>
/// A finished job's hand-off to EDMVault: which files to deliver, the document type, and the
/// collected property bag to turn into an index file.
/// </summary>
public sealed record EdmVaultHandoverRequest(
    string FilePrefix,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    string? DocumentType,
    PropertyBag Properties,
    string HandoverDirectory,
    EdmVaultIndexFormat IndexFormat,
    bool MoveFiles);

public enum EdmVaultIndexFormat
{
    Json,
    Xml,
}

/// <summary>
/// Delivers a finished job to EDMVault. The default implementation
/// (<see cref="FileDrop.FileDropEdmVaultHandoverWriter"/>) drops files plus an index file into a
/// hand-over directory; <see cref="RestApi.RestApiEdmVaultHandoverWriter"/> instead calls the
/// EDMVault REST API directly - either way, <see cref="EdmVaultExportModule"/> itself stays
/// unaware of which connector is configured.
/// </summary>
public interface IEdmVaultHandoverWriter
{
    Task WriteAsync(EdmVaultHandoverRequest request, CancellationToken cancellationToken);
}
