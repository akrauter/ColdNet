using ColdNet.Core.Domain;
using ColdNet.Core.ExportImport;
using Xunit;

namespace ColdNet.Core.Tests;

public class ProcessExportImportTests
{
    [Fact]
    public void ExportAndImportChain_preserves_all_properties_and_assigns_new_ids()
    {
        var originalGroupId = Guid.NewGuid();
        var targetGroupId = Guid.NewGuid();

        var originalChain = new ProcessChain
        {
            Id = Guid.NewGuid(),
            ProcessGroupId = originalGroupId,
            Name = "Invoice Pipeline",
            Description = "Processes incoming PDF invoices",
            Enabled = true,
            IsRunning = false,
            WorkerName = "worker-1",
            JobsPerStep = 25,
            Modules =
            [
                new ModuleInstance
                {
                    Id = Guid.NewGuid(),
                    Order = 0,
                    ModuleTypeName = "ColdImport",
                    DisplayName = "Import Invoices",
                    Enabled = true,
                    CommonSettings = new CommonModuleSettings
                    {
                        Directory = "C:/Input",
                        OutputDirectory = "C:/Work",
                        FileExtension = "pdf",
                        OutputFileExtension = "pdf",
                        Save = true,
                        DeleteSourceFile = false,
                        MaskForDms = true,
                        Append = false
                    },
                    DmsSupport = new DmsSupportSettings
                    {
                        Enabled = true,
                        DocumentType = "Rechnung"
                    },
                    SettingsJson = """{"FileMask":"INV_*.pdf","TimeoutSeconds":30}"""
                },
                new ModuleInstance
                {
                    Id = Guid.NewGuid(),
                    Order = 1,
                    ModuleTypeName = "BarcodeSplit",
                    DisplayName = "Split Barcodes",
                    Enabled = true,
                    CommonSettings = new CommonModuleSettings
                    {
                        FileExtension = "pdf",
                        DeleteSourceFile = true
                    },
                    DmsSupport = new DmsSupportSettings(),
                    SettingsJson = """{"BarcodeType":"Code128"}"""
                }
            ]
        };

        // Export
        var json = ProcessExportImportService.ExportChain(originalChain);
        Assert.NotNull(json);
        Assert.Contains("Invoice Pipeline", json);
        Assert.Contains("coldnet-chain-v1", json);
        Assert.Contains("ColdImport", json);
        Assert.Contains("BarcodeSplit", json);

        // Detect
        var type = ProcessExportImportService.DetectPackageType(json);
        Assert.Equal(ImportPackageType.Chain, type);

        // Import
        var importResult = ProcessExportImportService.Import(json, targetGroupId);
        Assert.True(importResult.Success);
        Assert.Equal(ImportPackageType.Chain, importResult.Type);
        Assert.NotNull(importResult.Chain);

        var imported = importResult.Chain;
        Assert.NotEqual(originalChain.Id, imported.Id);
        Assert.Equal(targetGroupId, imported.ProcessGroupId);
        Assert.Equal(originalChain.Name, imported.Name);
        Assert.Equal(originalChain.Description, imported.Description);
        Assert.Equal(originalChain.Enabled, imported.Enabled);
        Assert.Equal(originalChain.IsRunning, imported.IsRunning);
        Assert.Equal(originalChain.WorkerName, imported.WorkerName);
        Assert.Equal(originalChain.JobsPerStep, imported.JobsPerStep);

        Assert.Equal(2, imported.Modules.Count);

        var mod0 = imported.Modules[0];
        Assert.NotEqual(originalChain.Modules[0].Id, mod0.Id);
        Assert.Equal(imported.Id, mod0.ProcessChainId);
        Assert.Equal(0, mod0.Order);
        Assert.Equal("ColdImport", mod0.ModuleTypeName);
        Assert.Equal("Import Invoices", mod0.DisplayName);
        Assert.True(mod0.Enabled);
        Assert.Equal("C:/Input", mod0.CommonSettings.Directory);
        Assert.Equal("C:/Work", mod0.CommonSettings.OutputDirectory);
        Assert.Equal("pdf", mod0.CommonSettings.FileExtension);
        Assert.True(mod0.CommonSettings.Save);
        Assert.False(mod0.CommonSettings.DeleteSourceFile);
        Assert.True(mod0.CommonSettings.MaskForDms);
        Assert.True(mod0.DmsSupport.Enabled);
        Assert.Equal("Rechnung", mod0.DmsSupport.DocumentType);
        Assert.Contains("INV_*.pdf", mod0.SettingsJson);

        var mod1 = imported.Modules[1];
        Assert.NotEqual(originalChain.Modules[1].Id, mod1.Id);
        Assert.Equal(imported.Id, mod1.ProcessChainId);
        Assert.Equal(1, mod1.Order);
        Assert.Equal("BarcodeSplit", mod1.ModuleTypeName);
        Assert.True(mod1.CommonSettings.DeleteSourceFile);
        Assert.Contains("Code128", mod1.SettingsJson);
    }

    [Fact]
    public void ExportAndImportGroup_preserves_all_chains_and_modules()
    {
        var originalGroup = new ProcessGroup
        {
            Id = Guid.NewGuid(),
            Name = "Accounting Group",
            ShortName = "ACC",
            Description = "All accounting related process chains",
            IsDefault = true,
            Chains =
            [
                new ProcessChain
                {
                    Id = Guid.NewGuid(),
                    Name = "Chain 1",
                    WorkerName = "default",
                    Modules =
                    [
                        new ModuleInstance
                        {
                            Id = Guid.NewGuid(),
                            Order = 0,
                            ModuleTypeName = "ColdImport",
                            DisplayName = "Import 1"
                        }
                    ]
                },
                new ProcessChain
                {
                    Id = Guid.NewGuid(),
                    Name = "Chain 2",
                    WorkerName = "worker-custom",
                    Modules =
                    [
                        new ModuleInstance
                        {
                            Id = Guid.NewGuid(),
                            Order = 0,
                            ModuleTypeName = "ColdImport",
                            DisplayName = "Import 2"
                        },
                        new ModuleInstance
                        {
                            Id = Guid.NewGuid(),
                            Order = 1,
                            ModuleTypeName = "OfficeToPdf",
                            DisplayName = "Office 2"
                        }
                    ]
                }
            ]
        };

        var json = ProcessExportImportService.ExportGroup(originalGroup);
        Assert.NotNull(json);
        Assert.Contains("Accounting Group", json);
        Assert.Contains("coldnet-group-v1", json);

        var detectedType = ProcessExportImportService.DetectPackageType(json);
        Assert.Equal(ImportPackageType.Group, detectedType);

        var importResult = ProcessExportImportService.Import(json);
        Assert.True(importResult.Success);
        Assert.Equal(ImportPackageType.Group, importResult.Type);
        Assert.NotNull(importResult.Group);

        var imported = importResult.Group;
        Assert.NotEqual(originalGroup.Id, imported.Id);
        Assert.False(imported.IsDefault); // Imported groups are never default
        Assert.Equal(originalGroup.Name, imported.Name);
        Assert.Equal("ACC", imported.ShortName);
        Assert.Equal(originalGroup.Description, imported.Description);

        Assert.Equal(2, imported.Chains.Count);
        var chain1 = imported.Chains[0];
        Assert.NotEqual(originalGroup.Chains[0].Id, chain1.Id);
        Assert.Equal(imported.Id, chain1.ProcessGroupId);
        Assert.Equal("Chain 1", chain1.Name);
        Assert.Single(chain1.Modules);
        Assert.Equal(chain1.Id, chain1.Modules[0].ProcessChainId);

        var chain2 = imported.Chains[1];
        Assert.NotEqual(originalGroup.Chains[1].Id, chain2.Id);
        Assert.Equal(imported.Id, chain2.ProcessGroupId);
        Assert.Equal("Chain 2", chain2.Name);
        Assert.Equal("worker-custom", chain2.WorkerName);
        Assert.Equal(2, chain2.Modules.Count);
        Assert.Equal(chain2.Id, chain2.Modules[0].ProcessChainId);
        Assert.Equal(chain2.Id, chain2.Modules[1].ProcessChainId);
    }

    [Fact]
    public void Import_handles_invalid_or_unknown_json_gracefully()
    {
        var invalidJsonResult = ProcessExportImportService.Import("this is not json");
        Assert.False(invalidJsonResult.Success);
        Assert.NotNull(invalidJsonResult.ErrorMessage);

        var emptyJsonResult = ProcessExportImportService.Import("");
        Assert.False(emptyJsonResult.Success);

        var randomObjectResult = ProcessExportImportService.Import("""{"foo":"bar"}""");
        Assert.False(randomObjectResult.Success);
        Assert.Equal(ImportPackageType.Unknown, randomObjectResult.Type);
    }
}
