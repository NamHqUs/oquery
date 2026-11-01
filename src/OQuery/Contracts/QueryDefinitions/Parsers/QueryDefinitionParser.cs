using OQuery;
using OQuery.QueryBuilderDto.Parsers;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace OQuery.Contracts.QueryDefinitions.Parsers;

public static partial class QueryDefinitionParser
{
    public static QueryDefinition ParseFromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);

        var definition = new DeserializerBuilder()
            .Build()
            .Deserialize<YamlQueryDefinition>(yml)
            ?? throw new FormatException("The YAML query definition cannot be null.");

        return Parse(definition);
    }

    public static QueryDefinition Parse(YamlQueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new QueryDefinition
        {
            Entity = !string.IsNullOrWhiteSpace(definition.Entity)
                ? definition.Entity
                : throw new FormatException("The YAML query definition requires an entity."),
            Projection = ProjectionDefinitionParser.Parse(definition.Projection ?? []),
            Filter = ConditionDefinitionParser.Parse(definition.Filter),
            Sorts = SortDefinitionParser.Parse(definition.Sorts),
            Page = definition.Page,
            PageSize = definition.PageSize,
            Contexts = definition.Contexts?.Select(context =>
            {
                if (string.IsNullOrWhiteSpace(context.Name))
                    throw new FormatException("A query context requires a name.");
                if (context.Query is null)
                    throw new FormatException($"Query context [{context.Name}] requires a query.");

                return new QueryContextDefintion(
                    context.Name,
                    Parse(context.Query),
                    ConditionDefinitionParser.ParseJoinCondition(context.Condition),
                    context.JoinType ?? QueryJoinType.Left)
                {
                    Alias = context.Alias,
                    Recursive = context.Recursive
                };
            }).ToList()
        };
    }

    public static QueryDefinition ParseFromYml(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return ParseFromYml(File.ReadAllText(file.FullName));
    }

    public static QueryDefinition Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ParseJsonDefinition(document.RootElement);
    }

    private static QueryDefinition ParseJsonDefinition(JsonElement root)
    {
        var contexts = TryGetProperty(root, "contexts", out var contextsElement)
            ? contextsElement.ValueKind == JsonValueKind.Array
                ? contextsElement.EnumerateArray().Select(context =>
                {
                    var alias = TryGetProperty(context, "alias", out var aliasElement)
                        ? aliasElement.GetString()
                        : null;
                    var name = TryGetProperty(context, "name", out var nameElement)
                        ? nameElement.GetString()
                        : null;
                    name = !string.IsNullOrWhiteSpace(name) ? name : alias;
                    if (string.IsNullOrWhiteSpace(name))
                        throw new JsonException("A query context requires a name.");
                    if (!TryGetProperty(context, "query", out var queryElement) ||
                        queryElement.ValueKind != JsonValueKind.Object)
                        throw new JsonException($"Query context [{name}] requires a query object.");

                    IConditionDefintion? condition = null;
                    if (TryGetProperty(context, "condition", out var conditionElement) &&
                        conditionElement.ValueKind != JsonValueKind.Null)
                    {
                        condition = conditionElement.ValueKind == JsonValueKind.String
                            ? ConditionDefinitionParser.Parse(conditionElement.GetString())
                            : JsonSerializer.Deserialize<IConditionDefintion>(
                                conditionElement,
                                new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    }

                    var joinType = !TryGetProperty(context, "joinType", out var joinTypeElement)
                        ? QueryJoinType.Left
                        : ParseJoinType(joinTypeElement);

                    var recursive = false;
                    if (TryGetProperty(context, "recursive", out var recursiveElement))
                    {
                        if (recursiveElement.ValueKind is not JsonValueKind.True and
                            not JsonValueKind.False)
                            throw new JsonException(
                                $"Query context [{name}] recursive must be a boolean.");
                        recursive = recursiveElement.GetBoolean();
                    }

                    return new QueryContextDefintion(
                        name,
                        ParseJsonDefinition(queryElement),
                        condition,
                        joinType)
                    {
                        Alias = alias,
                        Recursive = recursive
                    };
                }).ToList()
                : throw new JsonException("The contexts value must be an array.")
            : null;

        var projectionElement = TryGetProperty(root, "projection", out var projection)
            ? projection
            : TryGetProperty(root, "fields", out var legacyProjection)
                ? legacyProjection
                : (JsonElement?)null;
        List<ProjectionDefinition> projections = [];
        if (projectionElement is { } projectionValue)
        {
            if (projectionValue.ValueKind != JsonValueKind.Array)
                throw new JsonException("The projection value must be an array.");

            projections = projectionValue.EnumerateArray()
                .Select(field => JsonSerializer.Deserialize<ProjectionDefinition>(
                    field.GetRawText(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new JsonException("A projection field cannot be null."))
                .Where(field => !field.IsNewLine)
                .ToList();
        }

        return new QueryDefinition
        {
            Entity = root.GetProperty("entity").GetString()
                ?? throw new JsonException("The entity value cannot be null."),
            Projection = projections,
            Filter = root.TryGetProperty("filter", out var filter)
                ? filter.ValueKind == JsonValueKind.String
                    ? ConditionDefinitionParser.Parse(filter.GetString() ?? string.Empty)
                    : JsonSerializer.Deserialize<ConditionDefinition>(
                        filter,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                : null,
            Sorts = root.TryGetProperty("sorts", out var sorts)
                ? sorts.EnumerateArray().Select(ParseSort).ToList()
                : [],
            Page = ReadInt(root, "page"),
            PageSize = ReadInt(root, "pageSize"),
            Contexts = contexts
        };
    }

    private static QueryJoinType ParseJoinType(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) &&
                Enum.IsDefined(typeof(QueryJoinType), value) => (QueryJoinType)value,
            JsonValueKind.String when Enum.TryParse<QueryJoinType>(
                element.GetString(), true, out var value) => value,
            _ => throw new JsonException("A query context join type is invalid.")
        };

    public static IFieldDefinition ParseField(string plainText) => FieldDefinitionParser.Parse(plainText);
    public static IFieldDefinition[] ParseExpression(string plainText) => FieldDefinitionParser.ParseExpression(plainText);
    public static List<ProjectionDefinition> ParseProjection(string plainText) => ProjectionDefinitionParser.Parse(plainText);
    public static ConditionDefinition? ParseFilter(string plainText) => ConditionDefinitionParser.Parse(plainText);
    public static IConditionDefintion? ParseJoinCondition(string plainText)
        => ConditionDefinitionParser.ParseJoinCondition(plainText);
    public static List<SortDefinition> ParseSorts(string plainText) => SortDefinitionParser.Parse(plainText);

    private static SortDefinition ParseSort(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()
                ?? throw new JsonException("A sort cannot be null.");
            var descending = value.StartsWith("-", StringComparison.Ordinal);
            var fieldText = descending ? value[1..] : value;

            return new SortDefinition(
                FieldDefinitionParser.Parse(fieldText),
                descending ? QuerySortDirection.Descending : QuerySortDirection.Ascending);
        }

        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("A sort must be a string or an object.");

        var field = TryGetProperty(element, "field", out var fieldElement)
            ? ProjectionDefinitionJsonConverter.ParseField(fieldElement)
            : throw new JsonException("A sort requires a field expression.");
        var direction = !TryGetProperty(element, "direction", out var directionElement)
            ? QuerySortDirection.Ascending
            : ParseSortDirection(directionElement);

        return new SortDefinition(field, direction);
    }

    private static QuerySortDirection ParseSortDirection(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) &&
                Enum.IsDefined(typeof(QuerySortDirection), value) => (QuerySortDirection)value,
            JsonValueKind.String when Enum.TryParse<QuerySortDirection>(
                element.GetString(), true, out var value) => value,
            _ => throw new JsonException("A sort direction is invalid.")
        };

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        => element.TryGetProperty(name, out value) ||
            element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
            throw new JsonException($"The {name} value must be an integer or null.");
        return value;
    }
}
