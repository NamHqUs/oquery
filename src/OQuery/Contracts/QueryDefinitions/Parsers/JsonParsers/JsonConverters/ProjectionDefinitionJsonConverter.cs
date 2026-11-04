using OQuery;
using OQuery.QueryBuilderDto.Parsers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OQuery.QueryDefinitions.Parsers.JsonConverters;

public sealed class ProjectionDefinitionJsonConverter : JsonConverter<ProjectionDefinition>
{
    public override ProjectionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("A projection field must be an object.");

        if (!TryGetProperty(root, "field", out var fieldElement))
            throw new JsonException("A projection requires a field.");
        var field = ParseField(fieldElement);
        var alias = TryGetProperty(root, "alias", out var aliasElement)
            ? aliasElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => aliasElement.GetString(),
                _ => throw new JsonException("A projection field alias must be a string or null.")
            }
            : null;

        return new ProjectionDefinition(field, alias);
    }

    public override void Write(Utf8JsonWriter writer, ProjectionDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var aliasPropertyName = options.PropertyNamingPolicy?.ConvertName(nameof(value.Alias))
            ?? nameof(value.Alias);
        writer.WritePropertyName("field");
        WriteField(writer, value.Field, options);
        if (value.Alias is null)
            writer.WriteNull(aliasPropertyName);
        else
            writer.WriteString(aliasPropertyName, value.Alias);
        writer.WriteEndObject();
    }

    internal static IFieldDefinition ParseField(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return FieldDefinitionParser.Parse(element.GetString()!);
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("A projection expression requires a kind.");

        if (TryGetProperty(element, "oper", out _) &&
            TryGetProperty(element, "field", out _) &&
            !TryGetProperty(element, "kind", out _))
            return ParseMathField(element);

        if (!TryGetProperty(element, "kind", out var kindElement) ||
            kindElement.ValueKind != JsonValueKind.String)
            throw new JsonException("A projection expression requires a kind.");

        var field = kindElement.GetString()?.ToLowerInvariant() switch
        {
            "field" => new FieldDefinition(ReadString(element, "field")),
            "value" => new ConstFieldDefinition(ReadValue(element, "value")),
            "math" => ParseMathField(element),
            "group" => ParseGroup(element),
            "function" => ParseFunction(element),
            "condition" => ParseCondition(element),
            _ => throw new JsonException($"Invalid projection expression kind [{kindElement.GetString()}].")
        };
        return TryGetProperty(element, "oper", out var operationElement)
            ? WithOperation(field, ParseGroupOperation(operationElement))
            : field;
    }

    private static IFieldDefinition ParseMathField(JsonElement element)
    {
        if (!TryGetProperty(element, "oper", out var operationElement) ||
            !TryGetProperty(element, "field", out var fieldElement))
            throw new JsonException("A math field requires an operator and field.");

        var operation = operationElement.ValueKind switch
        {
            JsonValueKind.Number when operationElement.TryGetInt32(out var number) &&
                Enum.IsDefined(typeof(MatchOperation), number) => (MatchOperation)number,
            JsonValueKind.String => ParseMathOperation(operationElement.GetString()!),
            _ => throw new JsonException("A math field operator is invalid.")
        };
        if (!operation.IsArithmetic())
            throw new JsonException("A math field requires a supported arithmetic operator.");

        return WithOperation(ParseField(fieldElement), operation);
    }

    private static IFieldDefinition WithOperation(IFieldDefinition field, MatchOperation operation)
        => field switch
        {
            FieldDefinition value => value with { Oper = operation },
            ConstFieldDefinition value => value with { Oper = operation },
            FunctionFieldDefinition value => value with { Oper = operation },
            GroupFieldDefintion value => value with { Oper = operation },
            ConditionFieldDefinition value => value with { Oper = operation },
            _ => throw new JsonException("A projection operator requires a supported field expression.")
        };

    private static MatchOperation ParseMathOperation(string value)
    {
        try
        {
            return MatchOperationParser.ParseArithmetic(value);
        }
        catch (FormatException)
        {
            if (Enum.TryParse<MatchOperation>(value, ignoreCase: true, out var operation) &&
                operation.IsArithmetic())
                return operation;

            throw new JsonException("A math field requires a supported arithmetic operator.");
        }
    }

    private static GroupFieldDefintion ParseGroup(JsonElement element)
    {
        var operation = TryGetProperty(element, "oper", out var operationElement)
            ? ParseGroupOperation(operationElement)
            : MatchOperation.Add;
        if (!TryGetProperty(element, "fields", out var fields))
            throw new JsonException("A projection group requires a fields array.");
        if (fields.ValueKind != JsonValueKind.Array)
            throw new JsonException("A projection group requires a fields array.");

        var parsedFields = fields.EnumerateArray().Select(ParseField).ToArray();
        return new GroupFieldDefintion(
            operation,
            parsedFields);
    }

    private static MatchOperation ParseGroupOperation(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var number) &&
            Enum.IsDefined(typeof(MatchOperation), number))
            return (MatchOperation)number;

        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()!;
            if (Enum.TryParse<MatchOperation>(value, ignoreCase: true, out var operation) &&
                Enum.IsDefined(typeof(MatchOperation), operation))
                return operation;

            try
            {
                return MatchOperationParser.Parse(value);
            }
            catch (FormatException exception)
            {
                throw new JsonException("A projection group requires a supported operator.", exception);
            }
        }

        throw new JsonException("A grouped projection operator is invalid.");
    }

    private static IFieldDefinition ParseFunction(JsonElement element)
        => new FunctionFieldDefinition(ReadString(element, "function"), ReadArguments(element));

    private static IFieldDefinition ParseCondition(JsonElement element)
    {
        if (!TryGetProperty(element, "condition", out var condition))
            throw new JsonException("A condition expression requires a condition.");

        var parsed = JsonSerializer.Deserialize<IConditionDefintion>(
            condition.GetRawText(),
            QueryDefinitionJsonParser.CreateSerializerOptions())
            ?? throw new JsonException("A condition expression cannot be null.");
        return new ConditionFieldDefinition(parsed);
    }

    private static IFieldDefinition[] ReadArguments(JsonElement element)
    {
        if (!TryGetProperty(element, "arguments", out var arguments) ||
            arguments.ValueKind != JsonValueKind.Array)
            throw new JsonException("A projection function requires an argument array.");
        return arguments.EnumerateArray().Select(ParseField).ToArray();
    }

    private static object? ReadValue(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            throw new JsonException($"A projection {name} is required.");

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => value.GetDecimal(),
            _ => throw new JsonException($"A projection {name} must be a scalar value.")
        };
    }

    private static string ReadString(JsonElement element, string name)
        => TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"A projection {name} must be a string.");

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);

    internal static void WriteField(
        Utf8JsonWriter writer,
        IFieldDefinition field,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (field.Oper != MatchOperation.Add)
            writer.WriteString("oper", field.Oper.ToSymbol());
        WriteFieldExpression(writer, field, options);
        writer.WriteEndObject();
    }

    private static void WriteFieldExpression(
        Utf8JsonWriter writer,
        IFieldDefinition field,
        JsonSerializerOptions options)
    {
        switch (field)
        {
            case FieldDefinition fieldDto:
                writer.WriteString("kind", "field");
                writer.WriteString("field", fieldDto.FieldName);
                return;
            case ConstFieldDefinition constant:
                writer.WriteString("kind", "value");
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, constant.Value);
                return;
            case FunctionFieldDefinition function:
                writer.WriteString("kind", "function");
                writer.WriteString("function", function.FunctionName);
                writer.WritePropertyName("arguments");
                writer.WriteStartArray();
                foreach (var argument in function.Arguments)
                    WriteField(writer, argument, options);
                writer.WriteEndArray();
                return;
            case GroupFieldDefintion group:
                writer.WriteString("kind", "group");
                writer.WritePropertyName("fields");
                writer.WriteStartArray();
                foreach (var item in group.Fields)
                    WriteField(writer, item, options);
                writer.WriteEndArray();
                return;
            case ConditionFieldDefinition condition:
                writer.WriteString("kind", "condition");
                writer.WritePropertyName("condition");
                JsonSerializer.Serialize<IConditionDefintion>(writer, condition.Condition, options);
                return;
            default:
                throw new JsonException("Unsupported grouped projection expression.");
        }
    }

}
