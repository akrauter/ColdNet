using System.Text.Json.Serialization;
using ColdNet.Core.Domain;

namespace ColdNet.Core.ExportImport;

public class ModuleExportDto
{
    public int Order { get; set; }

    public string ModuleTypeName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool Enabled { get; set; } = true;

    public CommonModuleSettings CommonSettings { get; set; } = new();

    public DmsSupportSettings DmsSupport { get; set; } = new();

    [JsonConverter(typeof(RawJsonElementConverter))]
    public string SettingsJson { get; set; } = "{}";
}
