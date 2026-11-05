namespace OQuery.QueryDefinitions.Parsers;

public static class QueryDefinitionOQueryParser
{
    public static QueryDefinition Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var blocks = ReadBlocks(text);
        if (blocks.Count == 0)
            throw new FormatException("An OQuery definition requires at least one query.");

        blocks = NormalizeBlocks(blocks);
        var queries = blocks.ToDictionary(
            block => block.Name,
            block => ExpandBlock(block, blocks, []),
            StringComparer.OrdinalIgnoreCase);
        var blockNames = blocks
            .Select(block => block.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referencedContexts = queries.Values
            .SelectMany(query => query.Joins ?? [])
            .Select(relation => relation.RefContext)
            .Concat(blocks
                .Select(block => block.Entity)
                .Where(blockNames.Contains))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rootBlock = blocks.FirstOrDefault(block => !referencedContexts.Contains(block.Name))
            ?? blocks[0];
        var root = queries[rootBlock.Name];
        var contexts = root.Contexts
            ?? throw new FormatException("The root OQuery definition cannot have null contexts.");

        foreach (var block in blocks)
        {
            if (ReferenceEquals(block, rootBlock))
            {
                if (block.BaseDefinition is not null)
                    contexts[block.Name] = ExpandBlock(block.BaseDefinition, blocks, []);
                continue;
            }
            if (!contexts.TryAdd(block.Name, queries[block.Name]))
                throw new FormatException($"The query context [{block.Name}] is defined more than once.");
        }

        AddImplicitRecursiveContext(root, rootBlock.Name);
        ValidateContextReferences(root);
        return root;
    }

    public static QueryDefinition Parse(string text, string contextName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);

        var blocks = ReadBlocks(text);
        if (blocks.Count == 0)
            throw new FormatException("An OQuery definition requires at least one query.");

        blocks = NormalizeBlocks(blocks);
        var queries = blocks.ToDictionary(
            block => block.Name,
            block => ExpandBlock(block, blocks, []),
            StringComparer.OrdinalIgnoreCase);
        if (!queries.TryGetValue(contextName, out var selected))
            throw new KeyNotFoundException($"Query context [{contextName}] was not found.");
        var selectedBlock = blocks.First(block =>
            string.Equals(block.Name, contextName, StringComparison.OrdinalIgnoreCase));

        var requiredContextNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingContextNames = new Queue<string>();
        pendingContextNames.Enqueue(contextName);
        if (selectedBlock.BaseDefinition is not null &&
            queries.ContainsKey(selectedBlock.BaseDefinition.Entity))
        {
            pendingContextNames.Enqueue(selectedBlock.BaseDefinition.Entity);
        }
        while (pendingContextNames.Count > 0)
        {
            var currentName = pendingContextNames.Dequeue();
            if (!requiredContextNames.Add(currentName) ||
                !queries.TryGetValue(currentName, out var current))
                continue;

            if (queries.ContainsKey(current.Entity))
                pendingContextNames.Enqueue(current.Entity);
            foreach (var relation in current.Joins ?? [])
            {
                if (queries.ContainsKey(relation.RefContext))
                    pendingContextNames.Enqueue(relation.RefContext);
            }
        }

        var contexts = blocks
            .Where(block =>
                requiredContextNames.Contains(block.Name) &&
                !string.Equals(block.Name, contextName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                block => block.Name,
                block => (QueryDefinitionBase)queries[block.Name],
                StringComparer.OrdinalIgnoreCase);
        if (selectedBlock.BaseDefinition is not null)
            contexts[selectedBlock.Name] = ExpandBlock(selectedBlock.BaseDefinition, blocks, []);

        return new QueryDefinition(selected.Entity)
        {
            Projections = selected.Projections,
            Filter = selected.Filter,
            Sorts = selected.Sorts,
            Take = selected.Take,
            Skip = selected.Skip,
            Joins = selected.Joins,
            Contexts = contexts,
        };
    }

    private static void AddImplicitRecursiveContext(QueryDefinition root, string rootName)
    {
        var relation = root.Joins?.FirstOrDefault(candidate =>
            string.Equals(candidate.RefContext, rootName, StringComparison.OrdinalIgnoreCase));
        if (relation is null || root.Contexts is null || root.Contexts.ContainsKey(rootName))
            return;

        var alias = string.IsNullOrWhiteSpace(relation.Alias) ? rootName : relation.Alias;
        var contextFieldAliases = root.Projections
            .Select(projection => (Field: projection.Field, projection.Alias))
            .Where(item => item.Field is FieldDefinition field
                && !field.FieldName.Contains('.')
                && !string.IsNullOrWhiteSpace(item.Alias))
            .ToDictionary(
                item => ((FieldDefinition)item.Field).FieldName,
                item => item.Alias!,
                StringComparer.OrdinalIgnoreCase);
        var projectionText = root.Projections
            .Select(projection => projection.ToText())
            .Select(text => System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"(?<![A-Za-z0-9_]){System.Text.RegularExpressions.Regex.Escape(alias)}\.",
                $"{rootName}.",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(text => System.Text.RegularExpressions.Regex.Replace(
                text,
                $@"(?<context>{System.Text.RegularExpressions.Regex.Escape(rootName)})\.(?<field>[A-Za-z_][A-Za-z0-9_]*)",
                match => $"{match.Groups["context"].Value}.{contextFieldAliases.GetValueOrDefault(match.Groups["field"].Value, match.Groups["field"].Value)}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        root.Contexts[rootName] = new QueryDefinition(root.Entity)
        {
            Projections = projectionText.SelectMany(ProjectionDefinitionParser.Parse).ToList(),
        };
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
        var rootName = definition.Contexts?.ContainsKey(definition.Entity) == true
            ? GetUniqueRootName(definition.Contexts)
            : definition.Entity;
        WriteBlock(lines, rootName, definition.Entity, definition, string.Empty);
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
        var relations = new List<JoinDefinition>();

        foreach (var clause in block.Clauses)
        {
            var value = clause.Value;
            switch (clause.Name)
            {
                case "where":
                    filter = QueryDefinitionJsonParser.ParseFilter(value);
                    break;
                case "sort":
                case "order":
                case "orderby":
                    sorts = QueryDefinitionJsonParser.ParseSorts(value);
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

        var definition = new QueryDefinition(block.Entity)
        {
            Projections = QueryDefinitionJsonParser.ParseProjection(block.Projection),
            Filter = filter,
            Sorts = sorts,
            Take = pageSize,
            Skip = skip,
            Joins = relations
        };
        return definition;
    }

    private static JoinDefinition ParseJoin(string value)
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
        var targetMatch = System.Text.RegularExpressions.Regex.Match(
            target,
            "^(?:\"(?<name>[^\"]+)\"|(?<name>[A-Za-z_][A-Za-z0-9_]*))" +
            "(?:\\s+(?:as\\s+)?(?<alias>[A-Za-z_][A-Za-z0-9_]*))?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!targetMatch.Success)
            throw new FormatException($"Invalid join target [{target}].");
        var name = targetMatch.Groups["name"].Value;
        var alias = targetMatch.Groups["alias"].Success
            ? targetMatch.Groups["alias"].Value
            : null;
        if (string.Equals(alias, "as", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Invalid join target [{target}].");
        }
        if (string.IsNullOrWhiteSpace(name))
            throw new FormatException("A join requires a context name.");

        return new JoinDefinition(
            name,
            Alias: alias,
            Condition: condition is null ? null : QueryDefinitionJsonParser.ParseJoinCondition(condition),
            JoinType: joinType);
    }

    private static List<QueryBlock> ReadBlocks(string text)
    {
        var blocks = new List<QueryBlock>();
        QueryBlock? current = null;
        foreach (var rawLine in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (rawLine.Length > 0 &&
                !char.IsWhiteSpace(rawLine[0]) &&
                !IsClauseLine(line))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0)
                    throw new FormatException($"Invalid OQuery header [{line}].");
                var header = line[..separator].Trim();
                var projection = line[(separator + 1)..].Trim();
                var headerMatch = System.Text.RegularExpressions.Regex.Match(
                    header,
                    "^(?:\"(?<nameQuoted>[^\"]+)\"|(?<name>[A-Za-z_][A-Za-z0-9_]*))" +
                    "(?:\\s+is\\s+(?:\"(?<entityQuoted>[^\"]+)\"|(?<entity>[A-Za-z_][A-Za-z0-9_]*)))?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!headerMatch.Success)
                    throw new FormatException($"Invalid OQuery header [{header}].");
                var name = headerMatch.Groups["nameQuoted"].Success
                    ? headerMatch.Groups["nameQuoted"].Value
                    : headerMatch.Groups["name"].Value;
                var entity = headerMatch.Groups["entityQuoted"].Success
                    ? headerMatch.Groups["entityQuoted"].Value
                    : headerMatch.Groups["entity"].Success
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

    private static List<QueryBlock> NormalizeBlocks(IReadOnlyList<QueryBlock> blocks)
    {
        var normalized = new List<QueryBlock>(blocks.Count);
        foreach (var group in blocks.GroupBy(block => block.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() == 1)
            {
                normalized.Add(group.Single());
                continue;
            }

            var candidates = group.ToArray();
            var declaredSource = candidates
                .FirstOrDefault(block => !string.Equals(
                    block.Name,
                    block.Entity,
                    StringComparison.OrdinalIgnoreCase));
            var selected = candidates[^1];
            if (declaredSource is null)
                throw new FormatException($"The query context [{selected.Name}] is defined more than once.");

            if (candidates.Any(block =>
                !string.Equals(block.Entity, block.Name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(block.Entity, declaredSource.Entity, StringComparison.OrdinalIgnoreCase)))
            {
                throw new FormatException(
                    $"The query context [{selected.Name}] is defined with conflicting source entities.");
            }

            if (string.Equals(selected.Entity, selected.Name, StringComparison.OrdinalIgnoreCase))
                selected.BaseDefinition = declaredSource;
            normalized.Add(selected);
        }

        return normalized;
    }

    private static QueryDefinition ExpandBlock(
        QueryBlock block,
        IReadOnlyList<QueryBlock> blocks,
        HashSet<QueryBlock> expansionStack)
    {
        var definition = ParseBlock(block);
        var wildcard = definition.Projections?.FirstOrDefault(IsWildcardProjection);
        if (wildcard is null)
            return definition;

        if (!expansionStack.Add(block))
            throw new FormatException($"Cyclic wildcard projection inheritance includes [{block.Name}].");

        var sourceBlock = block.BaseDefinition ??
            blocks.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, block.Entity, StringComparison.OrdinalIgnoreCase));
        if (sourceBlock is null)
            throw new FormatException(
                $"Wildcard projection in [{block.Name}] requires a declared source context.");

        var source = ExpandBlock(sourceBlock, blocks, expansionStack);
        expansionStack.Remove(block);

        var inheritedProjections = (source.Projections ?? [])
            .Select(projection => new ProjectionDefinition(
                new FieldDefinition(projection.Alias ?? projection.Field.ToText())));
        var projections = inheritedProjections
            .Concat(definition.Projections!.Where(projection => !ReferenceEquals(projection, wildcard)))
            .ToList();
        return new QueryDefinition(definition.Entity)
        {
            Projections = projections,
            Filter = definition.Filter,
            Sorts = definition.Sorts,
            Take = definition.Take,
            Skip = definition.Skip,
            Joins = definition.Joins,
        };
    }

    private static bool IsWildcardProjection(ProjectionDefinition projection)
        => projection.Field is FieldDefinition { FieldName: "*" };

    private static string StripComment(string line)
    {
        var quote = '\0';
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || line[index - 1] != '\\'))
                    quote = '\0';
                continue;
            }
            if (character is '\'' or '"')
                quote = character;
            else if (character == '#')
                return line[..index];
        }
        return line;
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
            ? FormatIdentifier(name)
            : $"{FormatIdentifier(name)} is {FormatIdentifier(entity)}";
        var projections = definition.Projections.Select(field => field.ToText()).ToArray();
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
            lines.Add($"{indent}  Where {definition.Filter.ToText().Replace(" <NULL>", string.Empty, StringComparison.Ordinal)}");
        if (definition.Sorts is { Count: > 0 })
            lines.Add($"{indent}  Order {string.Join(", ", definition.Sorts.Select(FormatSort))}");
        var skip = definition.Skip;
        if (skip is not null && skip > 0)
            lines.Add($"{indent}  Skip {skip}");
        if (definition.Take is not null)
            lines.Add($"{indent}  Take {definition.Take}");
        foreach (var relation in definition.Joins ?? [])
        {
            var joinType = relation.JoinType?.ToString() ?? nameof(QueryJoinType.Left);
            var join = $"{joinType} Join {FormatIdentifier(relation.RefContext)}";
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
            "^(where|order|orderby|skip|take|join)(?:\\s|$)|" +
            "^(left|inner|right|full)\\s+join\\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string FormatIdentifier(string value)
        => System.Text.RegularExpressions.Regex.IsMatch(
            value,
            "^[A-Za-z_][A-Za-z0-9_]*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string GetUniqueRootName(
        IReadOnlyDictionary<string, QueryDefinitionBase> contexts)
    {
        const string baseName = "Query";
        var name = baseName;
        var suffix = 1;
        while (contexts.ContainsKey(name))
            name = $"{baseName}{suffix++}";
        return name;
    }

    private static string FormatSort(SortDefinition sort)
        => $"{sort.Field.ToText()} {(sort.Direction == QuerySortDirection.Descending ? "DESC" : "ASC")}";

    private static void ValidateContextReferences(QueryDefinition definition)
    {
        foreach (var relation in definition.Joins ?? [])
            if (!(definition.Contexts?.ContainsKey(relation.RefContext) ?? false))
                throw new FormatException($"Query relation references unknown context [{relation.RefContext}].");
    }

    private sealed class QueryBlock(string name, string entity, string projection)
    {
        public string Name { get; } = name;
        public string Entity { get; } = entity;
        public string Projection { get; set; } = projection;
        public List<QueryClause> Clauses { get; } = [];
        public QueryBlock? BaseDefinition { get; set; }
    }

    private sealed class QueryClause(string name, string value)
    {
        public string Name { get; } = name;
        public string Value { get; set; } = value;
    }
}
