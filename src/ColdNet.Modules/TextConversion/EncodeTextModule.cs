using System.Text;
using ColdNet.Core.Modules;

namespace ColdNet.Modules.TextConversion;

public class EncodeTextSettings
{
    /// <summary>Source encoding name (e.g. "windows-1252", "utf-8"). Empty = auto-detect via BOM, falling back to UTF-8.</summary>
    public string? SourceEncoding { get; set; }

    /// <summary>Target encoding name. Defaults to UTF-8.</summary>
    public string TargetEncoding { get; set; } = "utf-8";

    /// <summary>Writes a byte-order-mark, required for some downstream consumers to recognize UTF-8.</summary>
    public bool WriteByteOrderMark { get; set; } = true;
}

/// <summary>
/// Re-encodes a text file - the ColdNet equivalent of CNENCODETXT. UTF-8 output should carry a
/// BOM or downstream components may misinterpret it; this module makes that explicit instead of
/// implicit.
/// </summary>
[ModuleDefinition("EncodeText", ModuleCategory.TextConversion, "Encode Text", "Converts a text file's character encoding, e.g. to UTF-8 with BOM.", OriginalModule = "CNENCODETXT", SettingsType = typeof(EncodeTextSettings))]
public class EncodeTextModule : IColdModule
{
    public async Task<ModuleExecutionResult> ExecuteAsync(ModuleExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<EncodeTextSettings>();
        var inputPath = context.GetInputPath();

        if (!File.Exists(inputPath))
        {
            return ModuleExecutionResult.Fail($"Input file not found: {inputPath}");
        }

        await context.BackupSourceFilesAsync(cancellationToken);

        Encoding sourceEncoding;
        byte[] rawBytes;
        try
        {
            rawBytes = await File.ReadAllBytesAsync(inputPath, cancellationToken);
            sourceEncoding = string.IsNullOrWhiteSpace(settings.SourceEncoding)
                ? DetectEncoding(rawBytes)
                : Encoding.GetEncoding(settings.SourceEncoding);
        }
        catch (Exception ex)
        {
            return ModuleExecutionResult.Fail($"Could not read source encoding: {ex.Message}");
        }

        var text = sourceEncoding.GetString(rawBytes);

        var targetEncoding = new UTF8Encoding(settings.WriteByteOrderMark);
        if (!string.Equals(settings.TargetEncoding, "utf-8", StringComparison.OrdinalIgnoreCase))
        {
            targetEncoding = null!;
        }

        var outputPath = context.GetOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (targetEncoding is not null)
        {
            await File.WriteAllTextAsync(outputPath, text, targetEncoding, cancellationToken);
        }
        else
        {
            var encoding = Encoding.GetEncoding(settings.TargetEncoding);
            await File.WriteAllTextAsync(outputPath, text, encoding, cancellationToken);
        }

        if (context.Common.DeleteSourceFile && !string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(inputPath);
        }

        return ModuleExecutionResult.Ok();
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        return Encoding.UTF8;
    }
}
