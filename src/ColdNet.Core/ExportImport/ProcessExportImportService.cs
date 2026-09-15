using System.Text.Json;
using System.Text.Json.Serialization;
using ColdNet.Core.Domain;

namespace ColdNet.Core.ExportImport;

public enum ImportPackageType
{
    Unknown,
    Chain,
    Group
}

public class ImportResult
{
    public bool Success { get; set; }
    public ImportPackageType Type { get; set; }
    public ProcessChain? Chain { get; set; }
    public ProcessGroup? Group { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class ProcessExportImportService
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ChainExportDto ToDto(ProcessChain chain)
    {
        return new ChainExportDto
        {
            Schema = "coldnet-chain-v1",
            Name = chain.Name,
            Description = chain.Description,
            Enabled = chain.Enabled,
            IsRunning = chain.IsRunning,
            WorkerName = string.IsNullOrWhiteSpace(chain.WorkerName) ? "default" : chain.WorkerName,
            JobsPerStep = chain.JobsPerStep <= 0 ? 10 : chain.JobsPerStep,
            Modules = (chain.Modules ?? [])
                .OrderBy(m => m.Order)
                .Select((m, index) => new ModuleExportDto
                {
                    Order = index,
                    ModuleTypeName = m.ModuleTypeName,
                    DisplayName = m.DisplayName,
                    Enabled = m.Enabled,
                    CommonSettings = new CommonModuleSettings
                    {
                        Directory = m.CommonSettings.Directory ?? string.Empty,
                        OutputDirectory = m.CommonSettings.OutputDirectory,
                        FileExtension = m.CommonSettings.FileExtension ?? string.Empty,
                        OutputFileExtension = m.CommonSettings.OutputFileExtension,
                        Save = m.CommonSettings.Save,
                        DeleteSourceFile = m.CommonSettings.DeleteSourceFile,
                        MaskForDms = m.CommonSettings.MaskForDms,
                        Append = m.CommonSettings.Append,
                    },
                    DmsSupport = new DmsSupportSettings
                    {
                        Enabled = m.DmsSupport.Enabled,
                        DocumentType = m.DmsSupport.DocumentType,
                    },
                    SettingsJson = string.IsNullOrWhiteSpace(m.SettingsJson) ? "{}" : m.SettingsJson
                }).ToList()
        };
    }

    public static GroupExportDto ToDto(ProcessGroup group)
    {
        return new GroupExportDto
        {
            Schema = "coldnet-group-v1",
            Name = group.Name,
            ShortName = string.IsNullOrWhiteSpace(group.ShortName)
                ? (group.Name.Length > 10 ? group.Name[..10] : group.Name)
                : group.ShortName,
            Description = group.Description,
            Chains = (group.Chains ?? []).Select(ToDto).ToList()
        };
    }

    public static string ExportChain(ProcessChain chain)
    {
        var dto = ToDto(chain);
        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    public static string ExportGroup(ProcessGroup group)
    {
        var dto = ToDto(group);
        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    public static ProcessChain FromDto(ChainExportDto dto, Guid targetGroupId = default)
    {
        var chain = new ProcessChain
        {
            Id = Guid.NewGuid(),
            ProcessGroupId = targetGroupId,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? "Imported Chain" : dto.Name,
            Description = dto.Description,
            Enabled = dto.Enabled,
            IsRunning = dto.IsRunning,
            WorkerName = string.IsNullOrWhiteSpace(dto.WorkerName) ? "default" : dto.WorkerName,
            JobsPerStep = dto.JobsPerStep <= 0 ? 10 : dto.JobsPerStep,
        };

        var order = 0;
        foreach (var modDto in dto.Modules ?? Enumerable.Empty<ModuleExportDto>())
        {
            chain.Modules.Add(new ModuleInstance
            {
                Id = Guid.NewGuid(),
                ProcessChainId = chain.Id,
                Order = order++,
                ModuleTypeName = modDto.ModuleTypeName ?? string.Empty,
                DisplayName = modDto.DisplayName,
                Enabled = modDto.Enabled,
                CommonSettings = modDto.CommonSettings ?? new CommonModuleSettings(),
                DmsSupport = modDto.DmsSupport ?? new DmsSupportSettings(),
                SettingsJson = string.IsNullOrWhiteSpace(modDto.SettingsJson) ? "{}" : modDto.SettingsJson
            });
        }

        return chain;
    }

    public static ProcessGroup FromDto(GroupExportDto dto)
    {
        var group = new ProcessGroup
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(dto.Name) ? "Imported Group" : dto.Name,
            ShortName = string.IsNullOrWhiteSpace(dto.ShortName)
                ? (string.IsNullOrWhiteSpace(dto.Name) ? "Imported" : (dto.Name.Length > 10 ? dto.Name[..10] : dto.Name))
                : dto.ShortName,
            Description = dto.Description,
            IsDefault = false,
        };

        foreach (var chainDto in dto.Chains ?? Enumerable.Empty<ChainExportDto>())
        {
            var chain = FromDto(chainDto, group.Id);
            group.Chains.Add(chain);
        }

        return group;
    }

    public static ImportPackageType DetectPackageType(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ImportPackageType.Unknown;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return ImportPackageType.Unknown;

            if (root.TryGetProperty("schema", out var schemaProp) || root.TryGetProperty("$schema", out schemaProp))
            {
                var schemaVal = schemaProp.GetString();
                if (schemaVal != null)
                {
                    if (schemaVal.Contains("chain", StringComparison.OrdinalIgnoreCase)) return ImportPackageType.Chain;
                    if (schemaVal.Contains("group", StringComparison.OrdinalIgnoreCase)) return ImportPackageType.Group;
                }
            }

            if (root.TryGetProperty("chains", out _) || root.TryGetProperty("Chains", out _))
            {
                return ImportPackageType.Group;
            }

            if (root.TryGetProperty("modules", out _) || root.TryGetProperty("Modules", out _))
            {
                return ImportPackageType.Chain;
            }

            return ImportPackageType.Unknown;
        }
        catch
        {
            return ImportPackageType.Unknown;
        }
    }

    public static ImportResult Import(string json, Guid targetGroupId = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ImportResult { Success = false, ErrorMessage = "JSON content is empty." };
        }

        try
        {
            var detectedType = DetectPackageType(json);
            if (detectedType == ImportPackageType.Group)
            {
                var groupDto = JsonSerializer.Deserialize<GroupExportDto>(json, SerializerOptions);
                if (groupDto == null || string.IsNullOrWhiteSpace(groupDto.Name))
                {
                    return new ImportResult { Success = false, Type = ImportPackageType.Group, ErrorMessage = "Invalid process group JSON." };
                }

                var group = FromDto(groupDto);
                return new ImportResult { Success = true, Type = ImportPackageType.Group, Group = group };
            }

            if (detectedType == ImportPackageType.Chain)
            {
                var chainDto = JsonSerializer.Deserialize<ChainExportDto>(json, SerializerOptions);
                if (chainDto == null || string.IsNullOrWhiteSpace(chainDto.Name))
                {
                    return new ImportResult { Success = false, Type = ImportPackageType.Chain, ErrorMessage = "Invalid process chain JSON." };
                }

                var chain = FromDto(chainDto, targetGroupId);
                return new ImportResult { Success = true, Type = ImportPackageType.Chain, Chain = chain };
            }

            return new ImportResult { Success = false, Type = ImportPackageType.Unknown, ErrorMessage = "Could not identify JSON as a valid Process Group or Process Chain export." };
        }
        catch (Exception ex)
        {
            return new ImportResult { Success = false, ErrorMessage = $"JSON Deserialization failed: {ex.Message}" };
        }
    }
}
