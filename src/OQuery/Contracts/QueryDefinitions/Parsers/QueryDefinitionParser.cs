using OQuery;
using OQuery.QueryBuilderDto.Parsers;
using OQuery.QueryDefinitions.Parsers.JsonConverters;
using System.Text.Json;

namespace OQuery.QueryDefinitions.Parsers;

public static partial class QueryDefinitionParser
{
    public static void ValidateContextReferences(QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ValidateContextReferences(
            definition,
            definition.Contexts?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static void ValidateContextReferences(
        QueryDefinition definition,
        HashSet<string> availableContexts)
    {
        foreach (var relation in definition.Relations ?? [])
        {
            if (!availableContexts.Contains(relation.RefContext))
                throw new FormatException(
                    $"Query relation references unknown context [{relation.RefContext}].");
        }

        foreach (var (name, context) in definition.Contexts ?? [])
        {
            var nestedAvailableContexts = new HashSet<string>(
                availableContexts,
                StringComparer.OrdinalIgnoreCase)
            {
                name
            };
            if (context is QueryDefinition nested)
            {
                if (nested.Contexts is not null)
                    foreach (var nestedName in nested.Contexts.Keys)
                        nestedAvailableContexts.Add(nestedName);
                ValidateContextReferences(nested, nestedAvailableContexts);
            }
        }
    }

    private static string Required(string? value, string message)
        => !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException(message);

    public static QueryDefinition Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ParseJsonDefinition(document.RootElement);
    }

    private static QueryDefinition ParseJsonDefinition(JsonElement root)
    {
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
                    JsonQueryDefinitionParser.CreateSerializerOptions())
                    ?? throw new JsonException("A projection field cannot be null."))
                .Where(field => !field.IsNewLine)
                .ToList();
        }

        var result = new QueryDefinition
        {
            Entity = root.GetProperty("entity").GetString()
                ?? throw new JsonException("The entity value cannot be null."),
            Projection = projections,
            Filter = root.TryGetProperty("filter", out var filter)
                ? filter.ValueKind == JsonValueKind.String
                    ? ConditionDefinitionParser.Parse(filter.GetString() ?? string.Empty)
                    : JsonSerializer.Deserialize<ConditionDefinition>(
                        filter,
                        JsonQueryDefinitionParser.CreateSerializerOptions())
                : null,
            Sorts = root.TryGetProperty("sorts", out var sorts) && sorts.ValueKind != JsonValueKind.Null
                ? sorts.EnumerateArray().Select(ParseSort).ToList()
                : [],
            Page = ReadInt(root, "page"),
            PageSize = ReadInt(root, "pageSize"),
            Skip = ReadInt(root, "skip"),
        };
        if (TryGetProperty(root, "contexts", out var contextsElement))
        {
            if (contextsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var context in contextsElement.EnumerateObject())
                    result.Contexts![context.Name] = ParseJsonDefinition(context.Value);
            }
            else if (contextsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var context in contextsElement.EnumerateArray())
                {
                    var name = TryGetProperty(context, "name", out var nameElement)
                        ? nameElement.GetString()
                        : TryGetProperty(context, "alias", out var aliasElement) ? aliasElement.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name))
                        throw new JsonException("A query context requires a name.");
                    var queryElement = TryGetProperty(context, "query", out var nested)
                        ? nested
                        : context;
                    var contextQuery = ParseJsonDefinition(queryElement);
                    if (TryGetProperty(context, "recursive", out var recursive))
                    {
                        if (recursive.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                            throw new JsonException($"Query context [{name}] recursive must be a boolean.");
                        contextQuery.Recursive = recursive.GetBoolean();
                    }
                    result.Contexts![name] = contextQuery;
                    result.Relations!.Add(new RelationDefinition(
                        name,
                        TryGetProperty(context, "alias", out var alias) ? alias.GetString() : null,
                        TryGetProperty(context, "condition", out var condition) ? ParseJsonCondition(condition) : null,
                        TryGetProperty(context, "joinType", out var join) ? ParseJoinType(join) : QueryJoinType.Left));
                }
            }
            else throw new JsonException("The contexts value must be an object or array.");
        }
        if (TryGetProperty(root, "relations", out var relations) &&
            relations.ValueKind == JsonValueKind.Array)
            foreach (var relation in relations.EnumerateArray())
                result.Relations!.Add(ParseJsonRelation(relation));
        ValidateContextReferences(result);
        FlattenContexts(result.Contexts!);
        ValidateContextReferences(result);
        return result;
    }

    private static void FlattenContexts(Dictionary<string, QueryDefinitionBase> contexts)
    {
        foreach (var context in contexts.Values.ToArray())
        {
            if (context is not QueryDefinition query || query.Contexts is not { Count: > 0 })
                continue;
            var children = query.Contexts.ToArray();
            query.Contexts.Clear();
            foreach (var child in children)
            {
                contexts.TryAdd(child.Key, child.Value);
                FlattenContextInto(contexts, child.Value);
            }
        }
    }

    private static void FlattenContextInto(
        Dictionary<string, QueryDefinitionBase> destination,
        QueryDefinitionBase context)
    {
        if (context is not QueryDefinition query || query.Contexts is not { Count: > 0 })
            return;
        var children = query.Contexts.ToArray();
        query.Contexts.Clear();
        foreach (var child in children)
        {
            destination.TryAdd(child.Key, child.Value);
            FlattenContextInto(destination, child.Value);
        }
    }

    private static IConditionDefintion? ParseJsonCondition(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? ConditionDefinitionParser.ParseJoinCondition(element.GetString())
            : element.ValueKind == JsonValueKind.Null ? null :
            JsonSerializer.Deserialize<IConditionDefintion>(
                element,
                JsonQueryDefinitionParser.CreateSerializerOptions());

    private static RelationDefinition ParseJsonRelation(JsonElement element) =>
        new(
            Required(TryGetProperty(element, "refContext", out var reference) ? reference.GetString() :
                TryGetProperty(element, "refContextId", out var legacy) ? legacy.GetString() : null,
                "A query relation requires a referenced context."),
            TryGetProperty(element, "alias", out var alias) ? alias.GetString() : null,
            TryGetProperty(element, "condition", out var condition) ? ParseJsonCondition(condition) : null,
            TryGetProperty(element, "joinType", out var join)
                ? join.ValueKind == JsonValueKind.Null ? null : ParseJoinType(join)
                : QueryJoinType.Left);

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
