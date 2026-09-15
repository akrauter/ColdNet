using System.Text.Json.Serialization;

namespace ColdNet.Core.ExportImport;

public class GroupExportDto
{
    [JsonPropertyOrder(-2)]
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "coldnet-group-v1";

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<ChainExportDto> Chains { get; set; } = [];
}
