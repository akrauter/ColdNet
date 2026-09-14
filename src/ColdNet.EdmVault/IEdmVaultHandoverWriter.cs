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
/// hand-over directory, the same pattern d.cold uses to hand finished jobs to d.3 hostimport.
/// Swap in a different implementation (e.g. calling a future EDMVault REST API) without touching
/// <see cref="EdmVaultExportModule"/> itself.
/// </summary>
public interface IEdmVaultHandoverWriter
{
    Task WriteAsync(EdmVaultHandoverRequest request, CancellationToken cancellationToken);
}
