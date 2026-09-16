using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Modules.PropertyExtraction;
using ColdNet.Modules.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Engine.Tests;

public class PlaceholderTests
{
    private static ModuleExecutionContext CreateContext(CommonModuleSettings? common = null, DmsSupportSettings? dmsSupport = null)
    {
        var job = new Job { FilePrefix = "BEAXH1TS60QW", WorkDirectory = @"C:\jobs\work" };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = common ?? new CommonModuleSettings { Directory = @"C:\in", FileExtension = "pdf" },
            DmsSupport = dmsSupport ?? new DmsSupportSettings(),
        };
        return new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);
    }

    [Fact]
    public void ShellExecute_ResolveArguments_substitutes_fileName_and_extension()
    {
        var context = CreateContext();
        var inputPath = context.GetInputPath();
        var outputPath = context.GetOutputPath();

        var result = ShellExecuteModule.ResolveArguments("--in {fileName} --ext {extension}", context, inputPath, outputPath);

        Assert.Equal("--in BEAXH1TS60QW.pdf --ext pdf", result);
    }

    [Fact]
    public void SetVariable_ResolvePlaceholders_substitutes_FileName_and_Extension()
    {
        var context = CreateContext();

        var result = SetVariableModule.ResolvePlaceholders("{FileName} / {Extension}", context);

        Assert.Equal("BEAXH1TS60QW.pdf / pdf", result);
    }
}
