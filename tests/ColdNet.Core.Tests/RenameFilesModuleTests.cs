using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.FileHandling;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Core.Tests;

public class RenameFilesModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-renamefiles-test-{Guid.NewGuid():N}");

    public RenameFilesModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ExecuteAsync_with_FromTo_extension_renames_a_single_known_file()
    {
        File.WriteAllText(Path.Combine(_dir, "ABC123.$pdf"), "dummy");

        var context = CreateContext("ABC123", """{"FromExtension":"$pdf","ToExtension":"pdf"}""");

        var result = await new RenameFilesModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_dir, "ABC123.pdf")));
        Assert.False(File.Exists(Path.Combine(_dir, "ABC123.$pdf")));
    }

    [Fact]
    public async Task ExecuteAsync_with_pattern_strips_the_dollar_import_marker_automatically()
    {
        File.WriteAllText(Path.Combine(_dir, "ABC123.$pdf"), "dummy");

        var context = CreateContext("ABC123", """{"NewFileNamePattern":"{JobPrefix}.{Extension}"}""");

        var result = await new RenameFilesModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_dir, "ABC123.pdf")));
    }

    [Fact]
    public async Task ExecuteAsync_with_pattern_renames_every_file_belonging_to_the_job()
    {
        File.WriteAllText(Path.Combine(_dir, "ABC123.$pdf"), "dummy");
        File.WriteAllText(Path.Combine(_dir, "ABC123.$att"), "dummy");

        var context = CreateContext("ABC123", """{"NewFileNamePattern":"{JobPrefix}.{Extension}"}""");

        var result = await new RenameFilesModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_dir, "ABC123.pdf")));
        Assert.True(File.Exists(Path.Combine(_dir, "ABC123.att")));
    }

    [Fact]
    public async Task ExecuteAsync_with_pattern_supports_a_prefix_and_a_padded_counter()
    {
        File.WriteAllText(Path.Combine(_dir, "ABC123.$pdf"), "dummy");

        var context = CreateContext("ABC123", """{"NewFileNamePattern":"OUT_{Counter}.{Extension}","CounterDigits":4,"CounterFillChar":"0"}""");

        var result = await new RenameFilesModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_dir, "OUT_0001.pdf")));
    }

    [Fact]
    public async Task ExecuteAsync_with_pattern_keeps_incrementing_the_counter_across_calls_on_the_same_module_instance()
    {
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir },
            SettingsJson = """{"NewFileNamePattern":"OUT_{Counter}.{Extension}","CounterDigits":3,"CounterFillChar":" "}""",
        };

        File.WriteAllText(Path.Combine(_dir, "JOB1.pdf"), "dummy");
        var context1 = new ModuleExecutionContext(new Job { FilePrefix = "JOB1", WorkDirectory = _dir }, new ProcessChain(), moduleInstance, NullLogger.Instance);
        await new RenameFilesModule().ExecuteAsync(context1, CancellationToken.None);

        File.WriteAllText(Path.Combine(_dir, "JOB2.pdf"), "dummy");
        var context2 = new ModuleExecutionContext(new Job { FilePrefix = "JOB2", WorkDirectory = _dir }, new ProcessChain(), moduleInstance, NullLogger.Instance);
        await new RenameFilesModule().ExecuteAsync(context2, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_dir, "OUT_  1.pdf")));
        Assert.True(File.Exists(Path.Combine(_dir, "OUT_  2.pdf")));
        Assert.Equal(2, moduleInstance.Counter);
    }

    [Fact]
    public async Task ExecuteAsync_with_pattern_fails_when_no_file_matches_the_job_prefix()
    {
        var context = CreateContext("MISSING", """{"NewFileNamePattern":"{JobPrefix}.{Extension}"}""");

        var result = await new RenameFilesModule().ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("MISSING", result.ErrorMessage);
    }

    private ModuleExecutionContext CreateContext(string filePrefix, string settingsJson)
    {
        var job = new Job { FilePrefix = filePrefix, WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir },
            SettingsJson = settingsJson,
        };
        return new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
