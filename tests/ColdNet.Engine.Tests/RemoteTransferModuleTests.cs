using ColdNet.Core.Domain;
using ColdNet.EdmVault;
using ColdNet.Engine.Modules;
using ColdNet.Modules.RemoteTransfer;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Engine.Tests;

/// <summary>
/// No live SFTP/FTPS server or EdmVault.Api instance is reachable from this test environment, so
/// these cover what's verifiable without one: module catalogue registration, and every
/// fail-fast/no-op path a misconfigured instance should take *before* it would attempt a network
/// connection. The actual wire protocol calls (SSH.NET / FluentFTP / EdmVault.Api HTTP) were
/// verified by compiling against their real APIs, not by exercising them here - a real SFTP/FTPS
/// server or EdmVault.Api instance should be used for a manual smoke test before relying on these
/// in production.
/// </summary>
public class RemoteTransferModuleTests
{
    [Fact]
    public void ModuleRegistry_discovers_the_remote_transfer_and_edmvault_import_modules()
    {
        var registry = new ModuleRegistry([typeof(SftpImportModule).Assembly, typeof(EdmVaultImportModule).Assembly]);

        var sftpImport = registry.Find("SftpImport");
        Assert.NotNull(sftpImport);
        Assert.True(sftpImport.IsImportModule);
        Assert.Equal(ColdNet.Core.Modules.ModuleCategory.Import, sftpImport.Category);
        Assert.Equal(typeof(SftpImportSettings), sftpImport.SettingsType);

        var sftpExport = registry.Find("SftpExport");
        Assert.NotNull(sftpExport);
        Assert.False(sftpExport.IsImportModule);
        Assert.Equal(ColdNet.Core.Modules.ModuleCategory.RemoteTransfer, sftpExport.Category);
        Assert.Equal(typeof(SftpExportSettings), sftpExport.SettingsType);

        var edmVaultImport = registry.Find("EdmVaultImport");
        Assert.NotNull(edmVaultImport);
        Assert.True(edmVaultImport.IsImportModule);
        Assert.Equal(ColdNet.Core.Modules.ModuleCategory.Import, edmVaultImport.Category);
        Assert.Equal(typeof(EdmVaultImportSettings), edmVaultImport.SettingsType);
    }

    [Fact]
    public async Task SftpImportModule_returns_no_jobs_when_local_directory_is_unset()
    {
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings(), // Directory left blank
            SettingsJson = """{"Host":"sftp.example.com"}""",
        };

        var result = await new SftpImportModule().DiscoverJobsAsync(chain, moduleInstance, NullLogger.Instance, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SftpImportModule_returns_no_jobs_when_host_is_unset()
    {
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = Path.GetTempPath() },
            SettingsJson = "{}", // no Host configured
        };

        var result = await new SftpImportModule().DiscoverJobsAsync(chain, moduleInstance, NullLogger.Instance, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SftpExportModule_fails_fast_when_host_is_unset()
    {
        var job = new Job { FilePrefix = "ABC", WorkDirectory = Path.GetTempPath() };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = Path.GetTempPath() },
            SettingsJson = "{}", // no Host configured
        };
        var context = new ColdNet.Core.Modules.ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var result = await new SftpExportModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Host", result.ErrorMessage);
    }

    [Fact]
    public async Task SftpExportModule_fails_when_no_files_match_the_source_mask()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coldnet-sftpexport-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var job = new Job { FilePrefix = "NOFILE", WorkDirectory = tempDir };
            var chain = new ProcessChain();
            var moduleInstance = new ModuleInstance
            {
                CommonSettings = new CommonModuleSettings { Directory = tempDir },
                SettingsJson = """{"Host":"sftp.example.com"}""",
            };
            var context = new ColdNet.Core.Modules.ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

            var result = await new SftpExportModule().ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("No files matched", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task EdmVaultImportModule_returns_no_jobs_when_document_type_is_unset()
    {
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = Path.GetTempPath() },
            DmsSupport = new DmsSupportSettings(), // DocumentType left blank
        };

        // No DI dependencies are ever touched because the DocumentType check runs first.
        var module = new EdmVaultImportModule(
            httpClientFactory: null!,
            tokenProvider: null!,
            projectResolver: null!);

        var result = await module.DiscoverJobsAsync(chain, moduleInstance, NullLogger.Instance, CancellationToken.None);

        Assert.Empty(result);
    }
}
