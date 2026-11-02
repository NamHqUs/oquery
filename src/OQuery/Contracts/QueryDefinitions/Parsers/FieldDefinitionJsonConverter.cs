using System.Text.Json;
using System.Text.Json.Serialization;

namespace OQuery.QueryDefinitions.Parsers;

internal sealed class FieldDefinitionJsonConverter : JsonConverter<IFieldDefinition>
{
    public override IFieldDefinition Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new FieldDefinition(reader.GetString()
                ?? throw new JsonException("A filter field cannot be null."));

        using var document = JsonDocument.ParseValue(ref reader);
        return ProjectionDefinitionJsonConverter.ParseField(document.RootElement);
    }

    public override void Write(
        Utf8JsonWriter writer,
        IFieldDefinition value,
        JsonSerializerOptions options)
        => ProjectionDefinitionJsonConverter.WriteField(writer, value, options);
}
