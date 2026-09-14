using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
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

    [Fact]
    public void GetInputPath_uses_module_directory_and_extension()
    {
        var context = CreateContext(new CommonModuleSettings { Directory = @"C:\in", FileExtension = "pdf" });

        Assert.Equal(@"C:\in\ABC123.pdf", context.GetInputPath());
    }

    [Fact]
    public void GetInputPath_falls_back_to_job_work_directory_when_unset()
    {
        var context = CreateContext(new CommonModuleSettings { FileExtension = "txt" });

        Assert.Equal(@"C:\jobs\work\ABC123.txt", context.GetInputPath());
    }

    [Fact]
    public void GetOutputPath_falls_back_to_input_extension_when_output_extension_unset()
    {
        var context = CreateContext(new CommonModuleSettings { Directory = @"C:\in", FileExtension = "pdf" });

        Assert.Equal(@"C:\in\ABC123.pdf", context.GetOutputPath());
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

        Assert.Equal(@"C:\out\ABC123.tif", context.GetOutputPath());
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
