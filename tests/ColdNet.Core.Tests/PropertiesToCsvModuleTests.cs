using ColdNet.Core.Domain;
using ColdNet.Core.Modules;
using ColdNet.Core.Properties;
using ColdNet.Modules.Csv;
using Microsoft.Extensions.Logging.Abstractions;

namespace ColdNet.Core.Tests;

public class PropertiesToCsvModuleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"coldnet-propertiestocsv-test-{Guid.NewGuid():N}");

    public PropertiesToCsvModuleTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public async Task ExecuteAsync_writes_one_row_per_property_with_a_header_row()
    {
        var context = await CreateContextWithPropertiesAsync("DOC01", "{}", bag =>
        {
            bag.Set("InvoiceNumber", "4711");
            bag.Set("Customer", "Acme GmbH");
        });

        var result = await new PropertiesToCsvModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var outputPath = Path.Combine(_dir, "DOC01.csv");
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // UTF-8 BOM, for Excel's benefit
        var csv = await ReadCsvWithoutBomAsync(outputPath);
        Assert.Equal(
            "Name;Value\r\nInvoiceNumber;4711\r\nCustomer;Acme GmbH\r\n",
            csv);
    }

    [Fact]
    public async Task ExecuteAsync_writes_one_row_per_value_for_a_multi_value_field()
    {
        var context = await CreateContextWithPropertiesAsync("DOC02", """{"IncludeHeader":false}""", bag =>
        {
            bag.Add("LineItem", "Widget A");
            bag.Add("LineItem", "Widget B");
        });

        var result = await new PropertiesToCsvModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var csv = await ReadCsvWithoutBomAsync(Path.Combine(_dir, "DOC02.csv"));
        Assert.Equal("LineItem;Widget A\r\nLineItem;Widget B\r\n", csv);
    }

    [Fact]
    public async Task ExecuteAsync_quotes_a_value_containing_the_delimiter()
    {
        var context = await CreateContextWithPropertiesAsync("DOC03", """{"IncludeHeader":false}""", bag =>
        {
            bag.Set("Customer", "Acme GmbH; Niederlassung Berlin");
        });

        var result = await new PropertiesToCsvModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var csv = await ReadCsvWithoutBomAsync(Path.Combine(_dir, "DOC03.csv"));
        Assert.Equal("Customer;\"Acme GmbH; Niederlassung Berlin\"\r\n", csv);
    }

    [Fact]
    public async Task ExecuteAsync_uses_a_custom_delimiter_and_adds_the_document_type_row()
    {
        var context = await CreateContextWithPropertiesAsync("DOC04", """{"Delimiter":",","IncludeHeader":false}""", bag =>
        {
            bag.Set("InvoiceNumber", "4711");
        }, documentType: "Rechnung");

        var result = await new PropertiesToCsvModule().ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var csv = await ReadCsvWithoutBomAsync(Path.Combine(_dir, "DOC04.csv"));
        Assert.Equal("DocumentType,Rechnung\r\nInvoiceNumber,4711\r\n", csv);
    }

    private async Task<ModuleExecutionContext> CreateContextWithPropertiesAsync(string filePrefix, string settingsJson, Action<PropertyBag> populate, string? documentType = null)
    {
        var job = new Job { FilePrefix = filePrefix, WorkDirectory = _dir };
        var chain = new ProcessChain();
        var moduleInstance = new ModuleInstance
        {
            CommonSettings = new CommonModuleSettings { Directory = _dir, OutputFileExtension = "csv" },
            DmsSupport = new DmsSupportSettings { DocumentType = documentType },
            SettingsJson = settingsJson,
        };
        var context = new ModuleExecutionContext(job, chain, moduleInstance, NullLogger.Instance);

        var bag = new PropertyBag();
        populate(bag);
        await context.SavePropertiesAsync(bag, CancellationToken.None);

        return context;
    }

    private static async Task<string> ReadCsvWithoutBomAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return System.Text.Encoding.UTF8.GetString(hasBom ? bytes[3..] : bytes);
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
