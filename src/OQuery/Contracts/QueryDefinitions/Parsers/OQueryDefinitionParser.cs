namespace OQuery.QueryDefinitions.Parsers;

public static class OQueryDefinitionParser
{
    public static QueryDefinition Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var blocks = ReadBlocks(text);
        if (blocks.Count == 0)
            throw new FormatException("An OQuery definition requires at least one query.");

        var queries = blocks.ToDictionary(
            block => block.Name,
            ParseBlock,
            StringComparer.OrdinalIgnoreCase);
        var referencedContexts = queries.Values
            .SelectMany(query => query.Relations ?? [])
            .Select(relation => relation.RefContext)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rootBlock = blocks.FirstOrDefault(block => !referencedContexts.Contains(block.Name))
            ?? blocks[0];
        var root = queries[rootBlock.Name];
        var contexts = root.Contexts
            ?? throw new FormatException("The root OQuery definition cannot have null contexts.");

        foreach (var block in blocks)
        {
            if (ReferenceEquals(block, rootBlock))
                continue;
            if (!contexts.TryAdd(block.Name, queries[block.Name]))
                throw new FormatException($"The query context [{block.Name}] is defined more than once.");
        }

        ValidateContextReferences(root);
        return root;
    }

    public static QueryDefinition Parse(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Parse(File.ReadAllText(file.FullName));
    }

    public static string Write(QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var lines = new List<string>();
        WriteBlock(lines, definition.Entity, definition.Entity, definition, string.Empty);
        foreach (var (name, context) in definition.Contexts ?? [])
            WriteBlock(lines, name, GetSourceEntity(context, name), context, string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static QueryDefinition ParseBlock(QueryBlock block)
    {
        ConditionDefinition? filter = null;
        var sorts = new List<SortDefinition>();
        int? pageSize = null;
        int? skip = null;
        var relations = new List<RelationDefinition>();

        foreach (var clause in block.Clauses)
        {
            var value = clause.Value;
            switch (clause.Name)
            {
                case "where":
                    filter = QueryDefinitionParser.ParseFilter(value);
                    break;
                case "sort":
                case "order":
                case "orderby":
                    sorts = QueryDefinitionParser.ParseSorts(value);
                    break;
                case "skip":
                    skip = ParseNonNegativeInt(value, "skip");
                    break;
                case "take":
                case "pagesize":
                case "limit":
                    pageSize = ParsePositiveInt(value, clause.Name);
                    break;
                case "join":
                    relations.Add(ParseJoin(value));
                    break;
                default:
                    throw new FormatException($"Unknown OQuery clause [{clause.Name}].");
            }
        }

        var definition = new QueryDefinition
        {
            Entity = block.Entity,
            Projection = QueryDefinitionParser.ParseProjection(block.Projection),
            Filter = filter,
            Sorts = sorts,
            PageSize = pageSize,
            Skip = skip,
            Relations = relations
        };
        return definition;
    }

    private static RelationDefinition ParseJoin(string value)
    {
        var joinType = QueryJoinType.Inner;
        var join = value.Trim();
        foreach (var candidate in Enum.GetValues<QueryJoinType>())
        {
            var prefix = $"{candidate} join ";
            if (!join.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            joinType = candidate;
            join = join[prefix.Length..].Trim();
            break;
        }

        if (join.StartsWith("join ", StringComparison.OrdinalIgnoreCase))
            join = join[5..].Trim();

        var onIndex = join.IndexOf(" on ", StringComparison.OrdinalIgnoreCase);
        var target = onIndex < 0 ? join.Trim() : join[..onIndex].Trim();
        var condition = onIndex < 0 ? null : join[(onIndex + 4)..].Trim();
        var targetParts = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = targetParts.FirstOrDefault();
        var alias = targetParts.Length > 1 ? targetParts[1] : null;
        if (targetParts.Length > 2 ||
            string.Equals(alias, "as", StringComparison.OrdinalIgnoreCase))
        {
            if (targetParts.Length == 3 &&
                string.Equals(targetParts[1], "as", StringComparison.OrdinalIgnoreCase))
                alias = targetParts[2];
            else
                throw new FormatException($"Invalid join target [{target}].");
        }
        if (string.IsNullOrWhiteSpace(name))
            throw new FormatException("A join requires a context name.");

        return new RelationDefinition(
            name,
            Alias: alias,
            Condition: condition is null ? null : QueryDefinitionParser.ParseJoinCondition(condition),
            JoinType: joinType);
    }

    private static List<QueryBlock> ReadBlocks(string text)
    {
        var blocks = new List<QueryBlock>();
        QueryBlock? current = null;
        foreach (var rawLine in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (rawLine.Length > 0 && !char.IsWhiteSpace(rawLine[0]))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0)
                    throw new FormatException($"Invalid OQuery header [{line}].");
                var header = line[..separator].Trim();
                var projection = line[(separator + 1)..].Trim();
                var headerMatch = System.Text.RegularExpressions.Regex.Match(
                    header,
                    "^(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\\s+is\\s+(?<entity>[A-Za-z_][A-Za-z0-9_]*))?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!headerMatch.Success)
                    throw new FormatException($"Invalid OQuery header [{header}].");
                var name = headerMatch.Groups["name"].Value;
                var entity = headerMatch.Groups["entity"].Success
                    ? headerMatch.Groups["entity"].Value
                    : name;
                current = new QueryBlock(name, entity, projection);
                blocks.Add(current);
                continue;
            }

            if (current is null)
                throw new FormatException($"A clause appears before a query header [{line}].");
            if (!IsClauseLine(line))
            {
                if (current.Clauses.Count > 0)
                    current.Clauses[^1].Value = $"{current.Clauses[^1].Value} {line}".Trim();
                else
                    current.Projection = $"{current.Projection} {line}".Trim();
                continue;
            }
            var typedJoin = System.Text.RegularExpressions.Regex.Match(
                line,
                "^(left|inner|right|full)\\s+join\\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (typedJoin.Success)
            {
                current.Clauses.Add(new(
                    "join",
                    $"{typedJoin.Groups[1].Value} join {typedJoin.Groups[2].Value}"));
                continue;
            }
            var clauseSeparator = line.IndexOf(' ');
            if (clauseSeparator <= 0)
                throw new FormatException($"Invalid OQuery clause [{line}].");
            current.Clauses.Add(new(
                line[..clauseSeparator].ToLowerInvariant(),
                line[(clauseSeparator + 1)..].Trim()));
        }

        return blocks;
    }

    private static int ParsePositiveInt(string value, string name)
        => int.TryParse(value, out var result) && result > 0
            ? result
            : throw new FormatException($"{name} must be a positive integer.");

    private static int ParseNonNegativeInt(string value, string name)
        => int.TryParse(value, out var result) && result >= 0
            ? result
            : throw new FormatException($"{name} must be a non-negative integer.");

    private static void WriteBlock(
        List<string> lines,
        string name,
        string entity,
        QueryDefinitionBase definition,
        string indent)
    {
        var header = string.Equals(name, entity, StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name} is {entity}";
        var projections = definition.Projection.Select(field => field.ToText()).ToArray();
        var projectionText = string.Join(", ", projections);
        if (projectionText.Length <= 120 || projections.Length < 2)
        {
            lines.Add($"{indent}{header}: {projectionText}");
        }
        else
        {
            lines.Add($"{indent}{header}: {projections[0]},");
            for (var index = 1; index < projections.Length; index++)
            {
                var suffix = index == projections.Length - 1 ? string.Empty : ",";
                lines.Add($"{indent}    {projections[index]}{suffix}");
            }
        }
        if (definition.Filter is not null)
            lines.Add($"{indent}  Where {definition.Filter.ToText()}");
        if (definition.Sorts is { Count: > 0 })
            lines.Add($"{indent}  Order {string.Join(", ", definition.Sorts.Select(FormatSort))}");
        var skip = definition.Skip ?? (
            definition.Page is not null && definition.PageSize is not null
                ? (definition.Page.Value - 1) * definition.PageSize.Value
                : null);
        if (skip is not null && skip > 0)
            lines.Add($"{indent}  Skip {skip}");
        if (definition.PageSize is not null)
            lines.Add($"{indent}  Take {definition.PageSize}");
        foreach (var relation in definition.Relations ?? [])
        {
            var joinType = relation.JoinType?.ToString() ?? nameof(QueryJoinType.Left);
            var join = $"{joinType} Join {relation.RefContext}";
            if (!string.IsNullOrWhiteSpace(relation.Alias))
                join += $" {relation.Alias}";
            if (relation.Condition is not null)
                join += $" On {relation.Condition.ToText()}";
            lines.Add($"{indent}  {join}");
        }
    }

    private static string GetSourceEntity(QueryDefinitionBase definition, string name)
        => definition.Entity;

    private static bool IsClauseLine(string line)
        => System.Text.RegularExpressions.Regex.IsMatch(
            line,
            "^(where|order|orderby|skip|take|join)\\b|" +
            "^(left|inner|right|full)\\s+join\\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string FormatSort(SortDefinition sort)
        => $"{sort.Field.ToText()} {(sort.Direction == QuerySortDirection.Descending ? "DESC" : "ASC")}";

    private static void ValidateContextReferences(QueryDefinition definition)
    {
        foreach (var relation in definition.Relations ?? [])
            if (!(definition.Contexts?.ContainsKey(relation.RefContext) ?? false))
                throw new FormatException($"Query relation references unknown context [{relation.RefContext}].");
    }

    private sealed class QueryBlock(string name, string entity, string projection)
    {
        public string Name { get; } = name;
        public string Entity { get; } = entity;
        public string Projection { get; set; } = projection;
        public List<QueryClause> Clauses { get; } = [];
    }

    private sealed class QueryClause(string name, string value)
    {
        public string Name { get; } = name;
        public string Value { get; set; } = value;
    }
}
