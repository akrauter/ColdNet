using System.Text.Json;
using System.Xml.Linq;

namespace ColdNet.EdmVault.FileDrop;

/// <summary>
/// Default, file-system-only EDMVault connector: copies/moves the job's files into a hand-over
/// directory and writes an index file (JSON or XML) with the document type and all collected
/// properties next to them - a "drop finished files + index file for pickup" pattern. An
/// EDMVault-side watcher/import job is expected to pick files up from
/// <see cref="EdmVaultHandoverRequest.HandoverDirectory"/>.
/// </summary>
public class FileDropEdmVaultHandoverWriter : IEdmVaultHandoverWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task WriteAsync(EdmVaultHandoverRequest request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.HandoverDirectory);

        foreach (var file in request.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(request.HandoverDirectory, Path.GetFileName(file));

            if (request.MoveFiles)
            {
                File.Move(file, target, overwrite: true);
            }
            else
            {
                File.Copy(file, target, overwrite: true);
            }
        }

        var indexPath = request.IndexFormat == EdmVaultIndexFormat.Json
            ? Path.Combine(request.HandoverDirectory, request.FilePrefix + ".index.json")
            : Path.Combine(request.HandoverDirectory, request.FilePrefix + ".index.xml");

        if (request.IndexFormat == EdmVaultIndexFormat.Json)
        {
            await WriteJsonIndexAsync(request, indexPath, cancellationToken);
        }
        else
        {
            await WriteXmlIndexAsync(request, indexPath, cancellationToken);
        }
    }

    private static async Task WriteJsonIndexAsync(EdmVaultHandoverRequest request, string indexPath, CancellationToken ct)
    {
        var payload = new
        {
            filePrefix = request.FilePrefix,
            documentType = request.DocumentType,
            files = request.SourceFiles.Select(Path.GetFileName),
            fields = request.Properties.Values,
        };

        await using var stream = File.Create(indexPath);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
    }

    private static async Task WriteXmlIndexAsync(EdmVaultHandoverRequest request, string indexPath, CancellationToken ct)
    {
        var root = new XElement("edmVaultDocument",
            new XAttribute("filePrefix", request.FilePrefix));

        if (!string.IsNullOrWhiteSpace(request.DocumentType))
        {
            root.SetAttributeValue("documentType", request.DocumentType);
        }

        var filesElement = new XElement("files");
        foreach (var file in request.SourceFiles)
        {
            filesElement.Add(new XElement("file", Path.GetFileName(file)));
        }

        root.Add(filesElement);

        var fieldsElement = new XElement("fields");
        foreach (var (key, values) in request.Properties.Values)
        {
            foreach (var value in values)
            {
                fieldsElement.Add(new XElement("field", new XAttribute("name", key), value));
            }
        }

        root.Add(fieldsElement);

        await using var stream = File.Create(indexPath);
        await new XDocument(root).SaveAsync(stream, SaveOptions.None, ct);
    }
}
