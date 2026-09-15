using System.Text.Json.Serialization;

namespace ColdNet.Core.ExportImport;

public class ChainExportDto
{
    [JsonPropertyOrder(-2)]
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "coldnet-chain-v1";

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public bool IsRunning { get; set; } = true;

    public string WorkerName { get; set; } = "default";

    public int JobsPerStep { get; set; } = 10;

    public List<ModuleExportDto> Modules { get; set; } = [];
}
