using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace OQuery.QueryDefinitions.Parsers;

internal sealed class ConditionDefinitionJsonConverter : JsonConverter<IConditionDefintion>
{
    public override IConditionDefintion Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return TryGetProperty(root, "filters", out _)
            ? JsonSerializer.Deserialize<ConditionDefinition>(root, options)
                ?? throw new JsonException("The filter group cannot be null.")
            : JsonSerializer.Deserialize<ConditionStatementDefinition>(root, options)
                ?? throw new JsonException("The filter cannot be null.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        IConditionDefintion value,
        JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}

internal sealed class ConditionDefintionJsonConverter : JsonConverter<ConditionDefinition>
{
    public override ConditionDefinition Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var logicalOperation = TryGetProperty(root, "logicalOperation", out var operation)
            ? operation.Deserialize<LogicalOperator>(options)
            : LogicalOperator.And;
        var filters = TryGetProperty(root, "filters", out var filterElement)
            ? filterElement.Deserialize<List<IConditionDefintion>>(options) ?? []
            : [];

        return new ConditionDefinition(logicalOperation, filters.ToArray());
    }

    public override void Write(
        Utf8JsonWriter writer,
        ConditionDefinition value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("logicalOperation", (int)value.LogicalOperation);
        writer.WritePropertyName("filters");
        JsonSerializer.Serialize(writer, value.Filters, options);
        writer.WriteEndObject();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}

internal sealed class ConditionStatementDefinitionJsonConverter : JsonConverter<ConditionStatementDefinition>
{
    public override ConditionStatementDefinition Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var leftElement = TryGetProperty(root, "left", out var left)
            ? left
            : TryGetProperty(root, "field", out left)
                ? left
                : throw new JsonException("A filter item requires a left field expression.");
        var leftField = ReadField(leftElement, options, "left");
        var hasOperation = TryGetProperty(root, "operation", out var operationElement);
        var operatorId = hasOperation && operationElement.ValueKind == JsonValueKind.String
            ? operationElement.GetString()
            : null;
        if (hasOperation &&
            (operationElement.ValueKind is not (JsonValueKind.Number or JsonValueKind.String) ||
            (operationElement.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(operatorId)))
           )
            throw new JsonException("A filter operation must be a numeric built-in operation or a custom operator ID.");
        var operation = !hasOperation
            ? BinaryOperator.Equals
            : operationElement.ValueKind == JsonValueKind.Number
                ? operationElement.Deserialize<BinaryOperator>(options)
                : TryParseBuiltinOperator(operatorId!, out var builtinOperation)
                    ? builtinOperation
                    : BinaryOperator.Equals;
        if (operatorId?.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase) == true)
            operatorId = null;
        var right = TryGetProperty(root, "right", out var rightElement)
            ? rightElement.ValueKind == JsonValueKind.Null
                ? null
                : ReadField(rightElement, options, "right")
            : TryGetProperty(root, "value", out var legacyValue)
                ? ReadLegacyValue(legacyValue)
                : null;
        LogicalOperator? logicalOperator = TryGetProperty(root, "logicalOperator", out var logicalElement) &&
            logicalElement.ValueKind != JsonValueKind.Null
                ? logicalElement.Deserialize<LogicalOperator>(options)
                : null;

        return new ConditionStatementDefinition(logicalOperator, leftField, operation, right)
        {
            Parameterized = TryGetProperty(root, "parameterized", out var parameterized) &&
                parameterized.ValueKind == JsonValueKind.True,
            OperatorId = operatorId
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ConditionStatementDefinition value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.LogicalOperator is { } logicalOperator)
            writer.WriteNumber("logicalOperator", (int)logicalOperator);
        else
            writer.WriteNull("logicalOperator");
        writer.WritePropertyName("left");
        JsonSerializer.Serialize<IFieldDefinition>(writer, value.Left, options);
        if (value.OperatorId is null)
            writer.WriteNumber("operation", (int)value.Operator);
        else
            writer.WriteString("operation", value.OperatorId);
        writer.WritePropertyName("right");
        if (value.Right is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize<IFieldDefinition>(writer, value.Right, options);
        writer.WriteBoolean("parameterized", value.Parameterized);
        writer.WriteEndObject();
    }

    private static IFieldDefinition ReadField(JsonElement element, JsonSerializerOptions options, string name)
        => element.ValueKind == JsonValueKind.String
            ? new FieldDefinition(element.GetString() ?? throw new JsonException($"A filter {name} cannot be null."))
            : JsonSerializer.Deserialize<IFieldDefinition>(element, options)
                ?? throw new JsonException($"A filter {name} expression cannot be null.");

    private static IFieldDefinition ReadLegacyValue(JsonElement element)
        => new ConstFieldDefinition(element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDecimal(),
            _ => throw new JsonException($"Unsupported legacy filter value [{element.ValueKind}].")
        });

    private static bool TryParseBuiltinOperator(string operatorId, out BinaryOperator operation)
    {
        const string prefix = "builtin:";
        if (!operatorId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            operation = default;
            return false;
        }

        if (!int.TryParse(
                operatorId.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value) ||
            !Enum.IsDefined(typeof(BinaryOperator), value))
            throw new JsonException($"Built-in filter operation [{operatorId}] is invalid.");

        operation = (BinaryOperator)value;
        return true;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
}
