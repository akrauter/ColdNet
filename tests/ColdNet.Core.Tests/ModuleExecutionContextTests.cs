using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Core.Tests;

public class ModuleExecutionContextTests
{
    private static ModuleExecutionContext CreateContext(CommonModuleSettings common)
    {
        var job = new Job { FilePrefix = "ABC123", WorkDirectory = @"C:\jobs\work" };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance { CommonSettings = common };
        return new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);
    }

    private class SettingsWithSecret
    {
        public string Host { get; set; } = string.Empty;

        [SensitiveValue]
        public string Password { get; set; } = string.Empty;
    }

    [Fact]
    public void GetSettings_decrypts_sensitive_fields_using_the_supplied_protector()
    {
        var protector = AesSecretProtector.FromBase64Key(AesSecretProtector.GenerateBase64Key());
        var job = new Job { FilePrefix = "ABC123", WorkDirectory = @"C:\jobs\work" };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            SettingsJson = $$"""{"Host":"sftp.example.com","Password":"{{protector.Protect("hunter2")}}"}""",
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance, protector);

        var settings = context.GetSettings<SettingsWithSecret>();

        Assert.Equal("sftp.example.com", settings.Host);
        Assert.Equal("hunter2", settings.Password);
    }

    [Fact]
    public void GetSettings_without_a_protector_treats_stored_value_as_plaintext()
    {
        var job = new Job { FilePrefix = "ABC123", WorkDirectory = @"C:\jobs\work" };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            SettingsJson = """{"Host":"sftp.example.com","Password":"hunter2"}""",
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var settings = context.GetSettings<SettingsWithSecret>();

        Assert.Equal("hunter2", settings.Password);
    }

    // Directory values below (@"C:\in" etc.) are arbitrary opaque strings fed straight into
    // Path.Combine, same as the production code does - never touching the real filesystem - so
    // expected paths must be built the same way (Path.Combine uses the OS-native separator) rather
    // than hardcoded with a literal backslash, or these fail on Linux (where Path.Combine still
    // joins with '/', leaving the literal backslashes from the input untouched).

    [Fact]
    public void GetInputPath_uses_module_directory_and_extension()
    {
        var context = CreateContext(new CommonModuleSettings { Directory = @"C:\in", FileExtension = "pdf" });

        Assert.Equal(Path.Combine(@"C:\in", "ABC123.pdf"), context.GetInputPath());
    }

    [Fact]
    public void GetInputPath_falls_back_to_job_work_directory_when_unset()
    {
        var context = CreateContext(new CommonModuleSettings { FileExtension = "txt" });

        Assert.Equal(Path.Combine(@"C:\jobs\work", "ABC123.txt"), context.GetInputPath());
    }

    [Fact]
    public void GetOutputPath_falls_back_to_input_extension_when_output_extension_unset()
    {
        var context = CreateContext(new CommonModuleSettings { Directory = @"C:\in", FileExtension = "pdf" });

        Assert.Equal(Path.Combine(@"C:\in", "ABC123.pdf"), context.GetOutputPath());
    }

    [Fact]
    public void GetOutputPath_uses_output_directory_and_extension_when_set()
    {
        var context = CreateContext(new CommonModuleSettings
        {
            Directory = @"C:\in",
            OutputDirectory = @"C:\out",
            FileExtension = "pdf",
            OutputFileExtension = "tif",
        });

        Assert.Equal(Path.Combine(@"C:\out", "ABC123.tif"), context.GetOutputPath());
    }

    [Theory]
    [InlineData("pdf", ".pdf")]
    [InlineData(".pdf", ".pdf")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeExtension_ensures_leading_dot(string? input, string expected)
    {
        Assert.Equal(expected, ModuleExecutionContext.NormalizeExtension(input));
    }
}
