using OQuery;
using OQuery.QueryBuilderDto.Parsers;
using OQuery.QueryDefinitions.Parsers.JsonConverters;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace OQuery.QueryDefinitions.Parsers;

public static partial class QueryDefinitionParser
{
    public static void ValidateContextReferences(QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Contexts is null)
            return;

        foreach (var relation in definition.Relations ?? [])
        {
            if (!definition.Contexts.ContainsKey(relation.RefContext))
                throw new FormatException(
                    $"Query relation references unknown context [{relation.RefContext}].");
        }

        foreach (var context in definition.Contexts.Values.OfType<QueryDefinition>())
            ValidateContextReferences(context);
    }

    public static string ToYml(QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        List<string> lines = [$"Entity: {definition.Entity}"];
        if (definition.Projection.Count > 0)
        {
            lines.Add("Projection:");
            lines.AddRange(definition.Projection.Select(field => $"  - {field.ToText()}"));
        }
        if (definition.Filter is not null)
            lines.Add($"Filter: {definition.Filter.ToText().Replace(" <NULL>", string.Empty, StringComparison.Ordinal)}");
        if (definition.Sorts.Count > 0)
            lines.Add($"Sorts: {string.Join(", ", definition.Sorts.Select(FormatSort))}");
        if (definition.Page is not null) lines.Add($"Page: {definition.Page}");
        if (definition.PageSize is not null) lines.Add($"PageSize: {definition.PageSize}");

        if (definition.Relations is { Count: > 0 })
        {
            lines.Add("ExtraRelations:");
            foreach (var relation in definition.Relations)
            {
                lines.Add($"  - Alias: {relation.Alias ?? relation.RefContext}");
                if (relation.Condition is not null)
                    lines.Add($"    Condition: \"{FormatCondition(relation.Condition)}\"");
                lines.Add($"    JoinType: {relation.JoinType}");
                lines.Add($"    RefContextId: {relation.RefContext}");
            }
        }

        if (definition.Contexts is { Count: > 0 })
        {
            lines.Add("Contexts:");
            var allContexts = new Dictionary<string, QueryDefinitionBase>(
                definition.Contexts!, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, context) in allContexts)
            {
                lines.Add($"  - Id: {name}");
                lines.Add($"    Name: {name}");
                lines.Add($"    Entity: {context.Entity}");
                if (context.Recursive) lines.Add("    Recursive: true");
                if (context.Projection.Count > 0)
                {
                    lines.Add("    Projection:");
                    lines.AddRange(context.Projection.Select(field => $"      - {field.ToText()}"));
                }
                if (context.Filter is not null)
                    lines.Add($"    Filter: {context.Filter.ToText().Replace(" <NULL>", string.Empty, StringComparison.Ordinal)}");
                if (context.Sorts.Count > 0)
                    lines.Add($"    Sorts: {string.Join(", ", context.Sorts.Select(FormatSort))}");
                if (context.Page is not null) lines.Add($"    Page: {context.Page}");
                if (context.PageSize is not null) lines.Add($"    PageSize: {context.PageSize}");
                if (context.Relations is { Count: > 0 })
                {
                    lines.Add("    ExtraRelations:");
                    foreach (var relation in context.Relations)
                    {
                        lines.Add($"      - Alias: {relation.Alias ?? relation.RefContext}");
                        if (relation.Condition is not null)
                            lines.Add($"        Condition: \"{FormatCondition(relation.Condition)}\"");
                        lines.Add($"        JoinType: {relation.JoinType}");
                        lines.Add($"        RefContextId: {relation.RefContext}");
                    }
                }
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatSort(SortDefinition sort) =>
        $"{sort.Field.ToText()} {(sort.Direction == QuerySortDirection.Descending ? "DESC" : "ASC")}";

    private static string FormatCondition(IConditionDefintion condition) => condition switch
    {
        ConditionDefinition group => string.Join(" ", group.Filters.Select(FormatCondition)),
        ConditionStatementDefinition statement when statement.Right is GroupFieldDefintion group &&
            statement.Operator is BinaryOperator.Between or BinaryOperator.NotBetween or BinaryOperator.In or BinaryOperator.NotIn =>
            $"{statement.Left.ToText()} {statement.Operator} <{string.Join(", ", group.Fields.Select(field => field.ToText()))}>",
        ConditionStatementDefinition statement => statement.ToText(),
        _ => condition.ToText()
    };

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

        var query = new QueryDefinition
        {
            Entity = !string.IsNullOrWhiteSpace(definition.Entity)
                ? definition.Entity
                : throw new FormatException("The YAML query definition requires an entity."),
            Projection = ProjectionDefinitionParser.Parse(definition.Projection ?? []),
            Filter = ConditionDefinitionParser.Parse(definition.Filter),
            Sorts = SortDefinitionParser.Parse(definition.Sorts),
            Page = definition.Page,
            PageSize = definition.PageSize,
            Contexts = ParseContexts(definition, out var relations)
        };
        query.Relations!.AddRange(relations);
        return query;
    }

    private static Dictionary<string, QueryDefinitionBase> ParseContexts(
        YamlQueryDefinition definition, out List<RelationDefinition> relations)
    {
        relations = [];
        if (definition.Contexts is null)
            return new(StringComparer.OrdinalIgnoreCase);

        if (definition.Contexts.Any(context => context.Query is null))
        {
            var yaml = definition.Contexts.ToDictionary(
                context => Required(context.Id, "A flat query context requires an id."));
            var contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase);
            foreach (var context in yaml.Values)
            {
                var name = Required(context.Name, "A flat query context requires a name.");
                var parsed = ParseFlatContext(context);
                if (contexts.TryGetValue(name, out var existing))
                {
                    continue;
                }

                contexts.Add(name, parsed);
            }
            foreach (var (id, context) in yaml)
                contexts[Required(context.Name, "A flat query context requires a name.")].Relations!.AddRange(
                    context.ExtraRelations?.Select(r => CreateRelation(r, contexts, yaml)) ?? []);
            relations.AddRange(definition.ExtraRelations?.Select(r => CreateRelation(r, contexts, yaml)) ?? []);
            return contexts;
        }

        var nested = definition.Contexts.Select(ParseNestedContext).ToDictionary(
            context => context.Name!, context => (QueryDefinitionBase)context.Query!,
            StringComparer.OrdinalIgnoreCase);
        relations.AddRange(definition.Contexts.Select(context => new RelationDefinition(
            Required(context.Name, "A query context requires a name."),
            context.Alias,
            ConditionDefinitionParser.ParseJoinCondition(context.Condition),
            context.JoinType ?? QueryJoinType.Left)));
        FlattenContexts(nested);
        return nested;
    }

    private static void FlattenContexts(Dictionary<string, QueryDefinitionBase> contexts)
    {
        foreach (var context in contexts.Values.ToArray())
        {
            if (context is not QueryDefinition query || query.Contexts is not { Count: > 0 })
                continue;
            var children = query.Contexts!.ToArray();
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
        var children = query.Contexts!.ToArray();
        query.Contexts.Clear();
        foreach (var child in children)
        {
            destination.TryAdd(child.Key, child.Value);
            FlattenContextInto(destination, child.Value);
        }
    }

    private static (string? Name, QueryDefinition? Query) ParseNestedContext(YamlQueryContextDefinition context)
    {
        if (string.IsNullOrWhiteSpace(context.Name))
            throw new FormatException("A query context requires a name.");
        if (context.Query is null)
            throw new FormatException($"Query context [{context.Name}] requires a query.");

        var query = Parse(context.Query);
        query.Recursive = context.Recursive;
        return (context.Name, query);
    }

    private static QueryDefinition ParseFlatContext(YamlQueryContextDefinition context)
    {
        if (string.IsNullOrWhiteSpace(context.Name))
            throw new FormatException("A query context requires a name.");
        var query = new YamlQueryDefinition
        {
            Entity = context.Entity,
            Projection = context.Projection,
            Filter = context.Filter,
            Sorts = context.Sorts,
            Page = context.Page,
            PageSize = context.PageSize
        };

        var parsed = Parse(query);
        parsed.Recursive = context.Recursive;
        return parsed;
    }
    private static RelationDefinition CreateRelation(
        YamlQueryRelationDefinition relation,
        IReadOnlyDictionary<string, QueryDefinitionBase> contexts,
        IReadOnlyDictionary<string, YamlQueryContextDefinition>? legacyContexts = null)
    {
        var id = Required(relation.RefContextId, "A query relation requires a referenced context id.");
        var reference = legacyContexts?.TryGetValue(id, out var legacyContext) == true
            ? Required(legacyContext.Name, "A flat query context requires a name.")
            : id;
        if (!contexts.ContainsKey(reference))
            throw new FormatException($"Query relation references unknown context [{id}].");

        return new RelationDefinition(reference, relation.Alias,
            ConditionDefinitionParser.ParseJoinCondition(relation.Condition),
            relation.JoinType ?? QueryJoinType.Left);
    }

    private static string Required(string? value, string message)
        => !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException(message);

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
            Sorts = root.TryGetProperty("sorts", out var sorts)
                ? sorts.EnumerateArray().Select(ParseSort).ToList()
                : [],
            Page = ReadInt(root, "page"),
            PageSize = ReadInt(root, "pageSize"),
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
            TryGetProperty(element, "joinType", out var join) ? ParseJoinType(join) : QueryJoinType.Left);

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
