using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColdNet.Core.ExportImport;

public class RawJsonElementConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return string.IsNullOrWhiteSpace(str) ? "{}" : str;
        }

        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.GetRawText();
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return "{}";
        }

        using var otherDoc = JsonDocument.ParseValue(ref reader);
        return otherDoc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        try
        {
            writer.WriteRawValue(value, skipInputValidation: true);
        }
        catch
        {
            writer.WriteStringValue(value);
        }
    }
}
