using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.Execution;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.Compilation.Visitors;

internal sealed class ContextBinding(string name, QueryDefinitionBase query, JoinDefinition relation)
{
    public string Name { get; } = name;
    public QueryDefinitionBase Query { get; } = query;
    public string? Alias => relation.Alias;
    public IConditionDefintion? Condition => relation.Condition;
    public QueryJoinType JoinType => relation.JoinType ?? QueryJoinType.Left;
}

internal sealed class QueryDefinitionVisitor(QueryContext queryContext)
{
    public TableExpression Eval(QueryDefinition definition)
    {
        if (definition.Contexts is not { Count: > 0 })
            throw new FormatException("A query-definition expression requires at least one context.");

        var contextBindings = (definition.Joins ?? []).Select(relation =>
        {
            if (!definition.Contexts!.TryGetValue(relation.RefContext, out var query))
                throw new FormatException($"Query relation references unknown context [{relation.RefContext}].");
            return new ContextBinding(relation.RefContext, query, relation);
        }).ToList();
        if (contextBindings.Count == 0)
            contextBindings.AddRange(definition.Contexts!.Select(pair =>
                new ContextBinding(pair.Key, pair.Value, new JoinDefinition(pair.Key))));
        var definitions = new Dictionary<string, ContextBinding>(StringComparer.OrdinalIgnoreCase);
        List<ContextBinding> cteDefinitions = [];
        HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (var context in contextBindings)
        {
            if (string.IsNullOrWhiteSpace(context.Name))
                throw new FormatException("A query context requires a name.");
            if (!aliases.Add(GetContextAlias(context)))
                throw new FormatException($"Duplicate query context alias [{GetContextAlias(context)}].");
            if (definitions.TryGetValue(context.Name, out var existing))
            {
                continue;
            }

            definitions.Add(context.Name, context);
            cteDefinitions.Add(context);
        }

        var definitionIndexes = cteDefinitions
            .Select((context, index) => (context.Name, index))
            .ToDictionary(entry => entry.Name, entry => entry.index, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < cteDefinitions.Count; index++)
        {
            var context = cteDefinitions[index];
            if (!definitionIndexes.TryGetValue(context.Query.Entity, out var dependencyIndex))
            {
                foreach (var relation in context.Query.Joins ?? [])
                {
                    if (!definitionIndexes.TryGetValue(relation.RefContext, out var relationIndex) ||
                        relationIndex == index)
                        continue;
                    if (relationIndex > index)
                        throw new FormatException(
                            $"Query context [{context.Name}] references [{relation.RefContext}], which must be declared first.");
                }
                continue;
            }
            if (dependencyIndex != index && dependencyIndex > index)
                throw new FormatException(
                    $"Query context [{context.Name}] references [{context.Query.Entity}], which must be declared first.");
        }

        if (TryBuildRecursiveQuery(definition, definitions, cteDefinitions, out var recursiveQuery))
            return recursiveQuery;

        var table = CreateSource(definition.Entity, definitions);
        var cteTables = new Dictionary<string, TableExpression>(StringComparer.OrdinalIgnoreCase);
        foreach (var context in contextBindings)
        {
            var alias = GetContextAlias(context);
            var cteFields = GetCteFields(
                definitions[context.Name],
                definitions,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var cteTable = table is CteTableExpression rootCte &&
                string.Equals(rootCte.Name, context.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(alias, context.Name, StringComparison.OrdinalIgnoreCase)
                    ? table
                    : new CteTableExpression(
                        context.Name,
                        CreateCteEntity(definitions[context.Name], definitions),
                        queryContext.GetNextAlias(),
                        cteFields);
            cteTables.Add(alias, cteTable);
            table.JoinedTables.Add(alias, cteTable);
        }

        foreach (var context in contextBindings.Where(context => context.Condition is not null))
        {
            var joinedTable = cteTables[GetContextAlias(context)];
            table.Joins.Add(new JoinExpression(
                joinedTable,
                RenderJoinType(context.JoinType),
                [new JoinConditionVisitor(queryContext, table, joinedTable, cteTables).Parse(context.Condition!)]));
        }

        foreach (var context in cteDefinitions)
        {
            var cteQuery = BuildQuery(context.Query, definitions);
            table.CommonTableExpressions.Add(new StandardCommonTableExpression(context.Name, cteQuery));
            foreach (var reference in contextBindings.Where(reference =>
                string.Equals(reference.Name, context.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var cteTable = cteTables[GetContextAlias(reference)];
                cteTable.SetMetadataFields(
                    cteQuery.ProjectionItems
                        .Select(item => new Field(item.Alias, item.Alias))
                        .ToArray());
            }
        }

        return BuildQueryBody(definition, table, definitions);
    }

    private bool TryBuildRecursiveQuery(
        QueryDefinitionBase definition,
        IReadOnlyDictionary<string, ContextBinding> definitions,
        IReadOnlyList<ContextBinding> contexts,
        out TableExpression query)
    {
        query = null!;
        var recursiveContexts = contexts
            .Where(context =>
                ContainsContextReference(context.Query, context.Name, context.Alias))
            .ToArray();
        if (recursiveContexts.Length == 0)
            return false;
        if (recursiveContexts.Length != 1 || contexts.Count != 1)
            throw new NotSupportedException("Only one recursive query context is supported.");
        if (!queryContext.SupportsRecursiveCte)
            throw new NotSupportedException("The selected SQL dialect does not support recursive CTEs.");

        var context = recursiveContexts[0];
        if (context.Alias is not null &&
            definition.Projections.Any(projection =>
                ContainsContextReference(
                    projection.Field,
                    context.Name,
                    context.Alias)))
            return BuildRecursiveShorthandQuery(definition, context, definitions, out query);

        var recursiveJoinFields = GetContextFieldNames(context.Condition, context.Name, context.Alias);
        foreach (var fieldName in recursiveJoinFields)
        {
            if (context.Query.Projections.Any(projection =>
                string.Equals(GetProjectionAlias(projection), fieldName, StringComparison.OrdinalIgnoreCase)))
                continue;
            context.Query.Projections.Add(new ProjectionDefinition(new FieldDefinition(fieldName), fieldName));
        }

        var anchorDefinition = new QueryDefinition(definition.Entity)
        {
            Projections = definition.Projections
                .Select(projection => ContainsContextReference(projection, context.Name, context.Alias)
                    ? new ProjectionDefinition(new ConstFieldDefinition(0), projection.Alias)
                    : projection)
                .Concat(recursiveJoinFields
                    .Where(fieldName => !definition.Projections.Any(projection =>
                        string.Equals(GetProjectionAlias(projection), fieldName, StringComparison.OrdinalIgnoreCase)))
                    .Select(fieldName => new ProjectionDefinition(new FieldDefinition(fieldName), fieldName)))
                .ToList(),
            Filter = definition.Filter,
            Sorts = definition.Sorts,
            Take = null,
            Skip = null
        };
        var anchor = BuildQuery(anchorDefinition, definitions);
        var cteFields = GetCteFields(
            context,
            definitions,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var cteEntity = CreateCteEntity(context, definitions);
        var recursiveCte = new CteTableExpression(
            context.Name,
            cteEntity,
            queryContext.GetNextAlias(),
            cteFields);
        var recursive = CreateSource(context.Query.Entity, definitions);
        recursive.JoinedTables[GetContextAlias(context)] = recursiveCte;
        recursive.JoinedTables[context.Name] = recursiveCte;
        recursive = BuildQueryBody(context.Query, recursive, definitions);
        if (context.Condition is not null)
        {
            recursive.Joins.Add(new JoinExpression(
                recursiveCte,
                RenderJoinType(context.JoinType),
                [new JoinConditionVisitor(
                    queryContext,
                    recursive,
                    recursiveCte,
                    new Dictionary<string, TableExpression>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        [GetContextAlias(context)] = recursiveCte
                    }).Parse(context.Condition)]));
        }

        ValidateRecursiveProjectionShape(
            anchor.ProjectionItems.ToArray(),
            recursive.ProjectionItems.ToArray(),
            cteFields);
        var result = new CteTableExpression(
            context.Name,
            cteEntity,
            queryContext.GetNextAlias(),
            cteFields);
        result.CommonTableExpressions.Add(new RecursiveCommonTableExpression(
            context.Name,
            anchor,
            recursive,
            cteFields.Select(field => field.Name).ToArray()));
        result.ProjectionItems = definition.Projections
            .Select(projection => GetProjectionAlias(projection))
            .Select(fieldName => new ProjectionItem(
                new FieldExpression(result.Alias, cteFields.First(field =>
                    string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))),
                fieldName))
            .ToList();
        query = result;
        return true;
    }

    private bool BuildRecursiveShorthandQuery(
        QueryDefinitionBase definition,
        ContextBinding context,
        IReadOnlyDictionary<string, ContextBinding> definitions,
        out TableExpression query)
    {
        var sourceFields = queryContext.GetFields(queryContext.GetEntity(context.Query.Entity));
        var recursiveFieldNames = GetContextFieldNames(context.Condition, context.Name, context.Alias);
        var rootFieldNames = GetUnqualifiedConditionFieldNames(context.Condition);
        var cteFields = sourceFields
            .Where(field =>
                rootFieldNames.Contains(field.Name) ||
                recursiveFieldNames.Contains(field.Name))
            .ToList();
        if (cteFields.Count == 0)
            throw new FormatException($"Recursive query context [{context.Name}] requires key fields.");

        context.Query.Projections.Clear();
        context.Query.Projections.AddRange(
            cteFields.Select(field =>
                new ProjectionDefinition(new FieldDefinition(field.Name), field.Name)));
        var cteEntity = new Entity(context.Name, cteFields, DbTable: context.Name);
        var anchorDefinition = new QueryDefinition(context.Query.Entity)
        {
            Projections = cteFields
                .Select(field => new ProjectionDefinition(new FieldDefinition(field.Name), field.Name))
                .ToList(),
            Filter = QueryDefinitionJsonParser.ParseFilter(
                string.Join(" AND ", recursiveFieldNames.Select(field => $"{field} is null")))
        };
        var anchor = BuildQuery(anchorDefinition, definitions);

        var recursiveDefinition = new QueryDefinition(context.Query.Entity)
        {
            Projections = cteFields
                .Select(field => new ProjectionDefinition(new FieldDefinition(field.Name), field.Name))
                .ToList(),
            Joins =
            [
                new JoinDefinition(
                    context.Name,
                    context.Alias,
                    QueryDefinitionJsonParser.ParseJoinCondition(
                        $"{string.Join(" AND ", recursiveFieldNames.Select(field => $"{field} = {context.Alias}.{rootFieldNames.First()}"))}"),
                    QueryJoinType.Inner)
            ]
        };
        var recursive = BuildQuery(recursiveDefinition, definitions);

        var result = new CteTableExpression(
            context.Name,
            cteEntity,
            "tree",
            cteFields);
        result.CommonTableExpressions.Add(new RecursiveCommonTableExpression(
            context.Name,
            anchor,
            recursive,
            cteFields.Select(field => field.Name).ToArray()));

        var member = queryContext.CreateTableContext(
            queryContext.GetEntity(context.Query.Entity),
            "member");
        var manager = queryContext.CreateTableContext(
            queryContext.GetEntity(context.Query.Entity),
            "manager");
        result.JoinedTables.Add(member.Alias, member);
        result.JoinedTables.Add(manager.Alias, manager);
        result.Joins.Add(new JoinExpression(
            member,
            "INNER JOIN",
            [new BinarySqlExpression(
                new FieldExpression(member.Alias, sourceFields.First(field =>
                    string.Equals(field.Name, "Id", StringComparison.OrdinalIgnoreCase))),
                "=",
                new FieldExpression(result.Alias, cteFields.First(field =>
                    string.Equals(field.Name, "Id", StringComparison.OrdinalIgnoreCase))),
                typeof(bool))]));
        result.Joins.Add(new JoinExpression(
            manager,
            "LEFT JOIN",
            [new BinarySqlExpression(
                new FieldExpression(manager.Alias, sourceFields.First(field =>
                    string.Equals(field.Name, "Id", StringComparison.OrdinalIgnoreCase))),
                "=",
                new FieldExpression(result.Alias, cteFields.First(field =>
                    string.Equals(field.Name, "ManagerId", StringComparison.OrdinalIgnoreCase))),
                typeof(bool))]));

        result.ProjectionItems = definition.Projections
            .Select(projection => new ProjectionItem(
                queryContext.ParseQueryExpression(
                    result,
                    QualifyRecursiveProjection(projection.Field, manager.Alias)),
                GetProjectionAlias(projection)))
            .ToList();
        query = result;
        return true;
    }

    private static IFieldDefinition QualifyRecursiveProjection(
        IFieldDefinition field,
        string managerAlias)
        => field switch
        {
            FieldDefinition value when !value.FieldName.Contains('.') =>
                new FieldDefinition(value.Oper, $"{managerAlias}.{value.FieldName}"),
            FunctionFieldDefinition function => new FunctionFieldDefinition(
                function.Oper,
                function.FunctionName,
                function.Arguments
                    .Select(argument => QualifyRecursiveProjection(argument, managerAlias))
                    .ToArray()),
            GroupFieldDefintion group => new GroupFieldDefintion(
                group.Oper,
                group.Fields
                    .Select(argument => QualifyRecursiveProjection(argument, managerAlias))
                    .ToArray()),
            ConditionFieldDefinition condition => new ConditionFieldDefinition(
                condition.Oper,
                QualifyRecursiveCondition(condition.Condition, managerAlias)),
            _ => field
        };

    private static IConditionDefintion QualifyRecursiveCondition(
        IConditionDefintion condition,
        string managerAlias)
        => condition switch
        {
            ConditionDefinition group => new ConditionDefinition(
                group.LogicalOperation,
                group.Filters
                    .Select(filter => QualifyRecursiveCondition(filter, managerAlias))
                    .ToArray()),
            ConditionStatementDefinition statement => new ConditionStatementDefinition(
                statement.LogicalOperator,
                QualifyRecursiveField(statement.Left, managerAlias),
                statement.Operator,
                statement.Right is null
                    ? null
                    : QualifyRecursiveField(statement.Right, managerAlias)),
            _ => condition
        };

    private static IFieldDefinition QualifyRecursiveField(
        IFieldDefinition field,
        string managerAlias)
        => QualifyRecursiveProjection(field, managerAlias);

    private static void ValidateRecursiveProjectionShape(
        IReadOnlyList<ProjectionItem> anchor,
        IReadOnlyList<ProjectionItem> recursive,
        IReadOnlyList<Field> columns)
    {
        if (anchor.Count != columns.Count || recursive.Count != columns.Count)
            throw new FormatException(
                "Recursive query anchor and recursive projections must have the same number of columns.");

        for (var index = 0; index < columns.Count; index++)
        {
            if (!string.Equals(anchor[index].Alias, columns[index].Name, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(recursive[index].Alias, columns[index].Name, StringComparison.OrdinalIgnoreCase))
                throw new FormatException(
                    $"Recursive query projection column {index + 1} does not match the CTE column order.");
        }
    }

    private static bool ContainsContextReference(QueryDefinitionBase definition, string contextName, string? contextAlias = null)
    {
        return definition.Projections.Any(projection =>
                ContainsContextReference(projection.Field, contextName, contextAlias)) ||
            (definition.Filter is not null && ContainsContextReference(definition.Filter, contextName, contextAlias)) ||
            (definition.Sorts?.Any(sort => ContainsContextReference(sort.Field, contextName, contextAlias)) ?? false);
    }

    private static bool ContainsContextReference(ProjectionDefinition projection, string contextName, string? contextAlias)
        => ContainsContextReference(projection.Field, contextName, contextAlias);

    private static IReadOnlySet<string> GetContextFieldNames(
        IConditionDefintion? condition,
        string contextName,
        string? contextAlias)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (condition is not null)
            CollectContextFieldNames(condition, contextName, contextAlias, fields);
        return fields;
    }

    private static IReadOnlySet<string> GetUnqualifiedConditionFieldNames(IConditionDefintion? condition)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (condition is not null)
            CollectUnqualifiedConditionFieldNames(condition, fields);
        return fields;
    }

    private static void CollectUnqualifiedConditionFieldNames(
        IConditionDefintion condition,
        ISet<string> fields)
    {
        switch (condition)
        {
            case ConditionDefinition group:
                foreach (var filter in group.Filters)
                    CollectUnqualifiedConditionFieldNames(filter, fields);
                break;
            case ConditionStatementDefinition statement:
                CollectUnqualifiedConditionFieldNames(statement.Left, fields);
                if (statement.Right is not null)
                    CollectUnqualifiedConditionFieldNames(statement.Right, fields);
                break;
        }
    }

    private static void CollectUnqualifiedConditionFieldNames(
        IFieldDefinition field,
        ISet<string> fields)
    {
        switch (field)
        {
            case FieldDefinition value when !value.FieldName.Contains('.'):
                fields.Add(value.FieldName);
                break;
            case FunctionFieldDefinition function:
                foreach (var argument in function.Arguments)
                    CollectUnqualifiedConditionFieldNames(argument, fields);
                break;
            case GroupFieldDefintion group:
                foreach (var item in group.Fields)
                    CollectUnqualifiedConditionFieldNames(item, fields);
                break;
            case ConditionFieldDefinition nested:
                CollectUnqualifiedConditionFieldNames(nested.Condition, fields);
                break;
        }
    }

    private static void CollectContextFieldNames(
        IConditionDefintion condition,
        string contextName,
        string? contextAlias,
        ISet<string> fields)
    {
        switch (condition)
        {
            case ConditionDefinition group:
                foreach (var filter in group.Filters)
                    CollectContextFieldNames(filter, contextName, contextAlias, fields);
                break;
            case ConditionStatementDefinition statement:
                CollectContextFieldNames(statement.Left, contextName, contextAlias, fields);
                if (statement.Right is not null)
                    CollectContextFieldNames(statement.Right, contextName, contextAlias, fields);
                break;
        }
    }

    private static void CollectContextFieldNames(
        IFieldDefinition field,
        string contextName,
        string? contextAlias,
        ISet<string> fields)
    {
        switch (field)
        {
            case FieldDefinition value:
                var separator = value.FieldName.IndexOf('.');
                if (separator > 0 &&
                    (string.Equals(value.FieldName[..separator], contextName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(value.FieldName[..separator], contextAlias, StringComparison.OrdinalIgnoreCase)))
                    fields.Add(value.FieldName[(separator + 1)..]);
                break;
            case FunctionFieldDefinition function:
                foreach (var argument in function.Arguments)
                    CollectContextFieldNames(argument, contextName, contextAlias, fields);
                break;
            case GroupFieldDefintion group:
                foreach (var item in group.Fields)
                    CollectContextFieldNames(item, contextName, contextAlias, fields);
                break;
            case ConditionFieldDefinition nested:
                CollectContextFieldNames(nested.Condition, contextName, contextAlias, fields);
                break;
        }
    }

    private static bool ContainsContextReference(IFieldDefinition field, string contextName, string? contextAlias = null)
        => field switch
        {
            FieldDefinition value => IsContextField(value.FieldName, contextName, contextAlias),
            FunctionFieldDefinition function => function.Arguments
                .Any(argument => ContainsContextReference(argument, contextName, contextAlias)),
            GroupFieldDefintion group => group.Fields
                .Any(value => ContainsContextReference(value, contextName, contextAlias)),
            ConditionFieldDefinition condition => ContainsContextReference(condition.Condition, contextName, contextAlias),
            _ => false
        };

    private static bool ContainsContextReference(
        IConditionDefintion condition,
        string contextName,
        string? contextAlias = null)
        => condition switch
        {
            ConditionDefinition group => group.Filters
                .Any(filter => ContainsContextReference(filter, contextName, contextAlias)),
            ConditionStatementDefinition statement =>
                ContainsContextReference(statement.Left, contextName, contextAlias) ||
                (statement.Right is not null &&
                    ContainsContextReference(statement.Right, contextName, contextAlias)),
            _ => false
        };

    private static bool IsContextField(string fieldName, string contextName, string? contextAlias = null)
    {
        var separator = fieldName.IndexOf('.');
        return separator > 0 &&
            (string.Equals(fieldName[..separator], contextName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(fieldName[..separator], contextAlias, StringComparison.OrdinalIgnoreCase));
    }

    private TableExpression CreateSource(
        string entityName,
        IReadOnlyDictionary<string, ContextBinding> definitions)
    {
        if (definitions.TryGetValue(entityName, out var context))
        {
            var fields = GetCteFields(context, definitions, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return new CteTableExpression(
                context.Name,
                CreateCteEntity(context, definitions),
                queryContext.GetNextAlias(),
                fields);
        }

        return queryContext.CreateTableContext(queryContext.GetEntity(entityName));
    }

    private Entity CreateCteEntity(
        ContextBinding context,
        IReadOnlyDictionary<string, ContextBinding> definitions)
    {
        var fields = GetCteFields(context, definitions, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return new Entity(context.Name, fields, DbTable: context.Name);
    }

    private List<Field> GetCteFields(
        ContextBinding context,
        IReadOnlyDictionary<string, ContextBinding> definitions,
        HashSet<string> visited)
    {
        if (!visited.Add(context.Name))
            throw new FormatException($"Query context dependency cycle detected at [{context.Name}].");
        var projections = context.Query.Projections.ToArray();
        if (projections.Length > 0)
            return projections.Select(projection =>
            {
                var name = GetProjectionAlias(projection);
                return new Field(name, name);
            }).ToList();
        if (definitions.TryGetValue(context.Query.Entity, out var sourceContext))
            return GetCteFields(sourceContext, definitions, visited);

        return queryContext.GetFields(queryContext.GetEntity(context.Query.Entity))
            .Select(field => new Field(field.Name, field.Name))
            .ToList();
    }

    private TableExpression BuildQuery(
        QueryDefinitionBase definition,
        IReadOnlyDictionary<string, ContextBinding> definitions)
    {
        var table = CreateSource(definition.Entity, definitions);
        table = AddContextJoins(definition, table, definitions);
        return BuildQueryBody(definition, table, definitions);
    }

    private TableExpression AddContextJoins(
        QueryDefinitionBase definition,
        TableExpression table,
        IReadOnlyDictionary<string, ContextBinding> definitions)
    {
        if (definition.Joins is not { Count: > 0 })
            return table;

        var contextTables = new Dictionary<string, TableExpression>(StringComparer.OrdinalIgnoreCase);
        foreach (var relation in definition.Joins)
        {
            if (!definitions.TryGetValue(relation.RefContext, out var context))
                throw new FormatException(
                    $"Query relation references unknown context [{relation.RefContext}].");

            var alias = GetContextAlias(context);
            var contextTable = new CteTableExpression(
                context.Name,
                CreateCteEntity(context, definitions),
                queryContext.GetNextAlias(),
                GetCteFields(context, definitions, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            contextTables.Add(alias, contextTable);
            table.JoinedTables.Add(alias, contextTable);
        }

        foreach (var relation in definition.Joins)
        {
            var context = definitions[relation.RefContext];
            var joinedTable = contextTables[GetContextAlias(context)];
            if (relation.Condition is null)
                continue;
            table.Joins.Add(new JoinExpression(
                joinedTable,
                RenderJoinType(relation.JoinType ?? QueryJoinType.Left),
                [new JoinConditionVisitor(queryContext, table, joinedTable, contextTables)
                    .Parse(relation.Condition)]));
        }

        return table;
    }

    private TableExpression BuildQueryBody(
        QueryDefinitionBase definition,
        TableExpression table,
        IReadOnlyDictionary<string, ContextBinding> definitions)
    {
        var projections = definition.Projections.ToArray();
        if (projections.Length > 0)
        {
            var projectionItems = new List<ProjectionItem>();
            foreach (var projection in projections)
            {
                foreach (var item in ExpandProjection(table, projection))
                {
                    projectionItems.Add(item);
                    table.ProjectionItems = projectionItems.ToArray();
                }
            }
        }
        else
        {
            table.EnsureDefaultProjection();
        }

        if (definition.Filter is not null)
            table = new WhereVisitor(table, queryContext)
                .Eval(DbQueryFromQueryDto.CreateFilter(definition.Filter));

        foreach (var sort in definition.Sorts ?? [])
            table.Orders.Add(new OrderExpression(
                queryContext.ParseQueryExpression(table, sort.Field),
                sort.Direction == QuerySortDirection.Descending));

        if (definition.Skip is < 0)
            throw new ArgumentOutOfRangeException(nameof(definition.Skip), "Skip cannot be negative.");
        if (definition.Take is < 0)
            throw new ArgumentOutOfRangeException(nameof(definition.Take), "Take cannot be negative.");

        if (definition.Take is > 0)
        {
            var take = definition.Take.Value;
            var skip = definition.Skip ?? 0;
            if (skip < 0)
                throw new ArgumentOutOfRangeException(nameof(definition.Skip), "Skip cannot be negative.");
            table = new SkipExpression(table, skip);
            table = new TakeExpression(table, take);
        }
        else if (definition.Skip is not null)
        {
            table = new SkipExpression(table, definition.Skip.Value);
        }

        return table;
    }

    private IEnumerable<ProjectionItem> ExpandProjection(
        TableExpression table,
        ProjectionDefinition projection)
    {
        if (projection.Field is FieldDefinition { FieldName: var fieldName } &&
            (fieldName == "*" || fieldName.EndsWith(".*", StringComparison.Ordinal)))
        {
            var wildcardTable = fieldName == "*"
                ? table
                : queryContext.ResolveTableContext(table, fieldName[..^2]);
            foreach (var field in queryContext.GetFields(wildcardTable))
                yield return new ProjectionItem(
                    new FieldExpression(wildcardTable.Alias, field),
                    field.Name);
            yield break;
        }

        yield return new ProjectionItem(
            queryContext.ParseQueryExpression(table, projection.Field),
            GetProjectionAlias(projection));
    }

    private static string GetProjectionAlias(ProjectionDefinition projection)
        => string.IsNullOrWhiteSpace(projection.Alias)
            ? projection.Field.ToText()
            : projection.Alias;

    private static string GetContextAlias(ContextBinding context)
        => string.IsNullOrWhiteSpace(context.Alias) ? context.Name : context.Alias;

    private static string RenderJoinType(QueryJoinType joinType)
        => joinType switch
        {
            QueryJoinType.Inner => "INNER JOIN",
            QueryJoinType.Left => "LEFT JOIN",
            QueryJoinType.Right => "RIGHT JOIN",
            QueryJoinType.Full => "FULL JOIN",
            _ => throw new ArgumentOutOfRangeException(nameof(joinType))
        };
}

internal sealed class JoinConditionVisitor(
    QueryContext queryContext,
    TableExpression leftTable,
    TableExpression rightTable,
    IReadOnlyDictionary<string, TableExpression> contextTables)
{
    public SqlExpression Parse(IConditionDefintion condition)
        => condition switch
        {
            ConditionDefinition group => ParseGroup(group),
            ConditionStatementDefinition statement => ParseStatement(statement),
            _ => throw new FormatException($"Unsupported join condition [{condition.GetType().Name}].")
        };

    private SqlExpression ParseGroup(ConditionDefinition group)
    {
        SqlExpression? result = null;
        foreach (var child in group.Filters)
        {
            var childExpression = Parse(child);
            if (result is null)
            {
                result = childExpression;
                continue;
            }

            var operation = child is ConditionStatementDefinition { LogicalOperator: { } logicalOperator }
                ? logicalOperator
                : group.LogicalOperation;
            result = new BinarySqlExpression(
                result,
                operation.ToString().ToUpperInvariant(),
                childExpression,
                typeof(bool));
        }

        return result ?? throw new FormatException("A join condition group cannot be empty.");
    }

    private SqlExpression ParseStatement(ConditionStatementDefinition statement)
    {
        var left = ParseExpression(statement.Left, leftTable);
        if (statement.Operator is BinaryOperator.IsNull or BinaryOperator.IsNotNull)
            return new PredicateExpression(
                left,
                statement.Operator == BinaryOperator.IsNull ? "IS NULL" : "IS NOT NULL");

        var rightDefinition = statement.Right
            ?? throw new FormatException($"Join operator [{statement.Operator}] requires a right operand.");

        if (statement.Operator is BinaryOperator.Between or BinaryOperator.NotBetween)
        {
            if (rightDefinition is not GroupFieldDefintion { Fields.Length: 2 } range)
                throw new FormatException("A join range condition requires exactly two bounds.");

            return new RangePredicateExpression(
                left,
                statement.Operator == BinaryOperator.Between ? "BETWEEN" : "NOT BETWEEN",
                ParseExpression(range.Fields[0], rightTable),
                ParseExpression(range.Fields[1], rightTable));
        }

        if (statement.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            if (rightDefinition is not GroupFieldDefintion { Fields.Length: > 0 } values)
                throw new FormatException("A join set condition requires at least one value.");

            return new SetPredicateExpression(
                left,
                statement.Operator == BinaryOperator.In ? "IN" : "NOT IN",
                values.Fields.Select(value => ParseExpression(value, rightTable)).ToArray());
        }

        var right = ParseExpression(rightDefinition, rightTable);
        if (statement.OperatorId is not null)
        {
            if (string.IsNullOrWhiteSpace(statement.CustomSqlTemplate))
                throw new FormatException($"Custom operator '{statement.OperatorId}' has no trusted SQL template.");
            return new TemplateCompareSqlExpression(left, right, statement.CustomSqlTemplate, typeof(bool));
        }

        var operation = statement.Operator switch
        {
            BinaryOperator.Equals => "=",
            BinaryOperator.NotEquals => "<>",
            BinaryOperator.Like => "LIKE",
            BinaryOperator.NotLike => "NOT LIKE",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.LessThan => "<",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LessThanOrEqual => "<=",
            _ => throw new FormatException($"Unsupported join operator [{statement.Operator}].")
        };
        return new BinarySqlExpression(left, operation, right, typeof(bool));
    }

    private SqlExpression ParseExpression(IFieldDefinition expression, TableExpression defaultTable)
        => expression switch
        {
            FieldDefinition field => ParseField(field.FieldName, defaultTable),
            GroupFieldDefintion group => ParseArithmetic(group.Fields, defaultTable),
            ConstFieldDefinition or FunctionFieldDefinition =>
                queryContext.ParseQueryExpression(defaultTable, expression),
            _ => throw new FormatException($"Unsupported join expression [{expression.GetType().Name}].")
        };

    private SqlExpression ParseField(string fieldPath, TableExpression defaultTable)
    {
        var separator = fieldPath.IndexOf('.');
        if (separator > 0)
        {
            var sourceName = fieldPath[..separator];
            var remainingPath = fieldPath[(separator + 1)..];
            if (string.Equals(sourceName, leftTable.Entity.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceName, leftTable.Alias, StringComparison.OrdinalIgnoreCase))
                return queryContext.ParseFieldPath(leftTable, remainingPath);
            if (string.Equals(sourceName, rightTable.Entity.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceName, rightTable.Alias, StringComparison.OrdinalIgnoreCase))
                return queryContext.ParseFieldPath(rightTable, remainingPath);
            if (contextTables.TryGetValue(sourceName, out var contextTable))
                return queryContext.ParseFieldPath(contextTable, remainingPath);
        }

        return queryContext.ParseFieldPath(defaultTable, fieldPath);
    }

    private SqlExpression ParseArithmetic(
        IReadOnlyList<IFieldDefinition> fields,
        TableExpression defaultTable)
    {
        if (fields.Count == 0 || fields[0].Oper != MatchOperation.Add)
            throw new FormatException("A join arithmetic expression requires a first operand.");

        var values = new Stack<SqlExpression>();
        var operations = new Stack<MatchOperation>();
        values.Push(ParseExpression(fields[0], defaultTable));

        for (var index = 1; index < fields.Count; index++)
        {
            var operation = fields[index].Oper;
            if (!operation.IsArithmetic())
                throw new FormatException($"Unsupported join expression operator [{operation}].");

            while (operations.Count > 0 &&
                GetPrecedence(operations.Peek()) >= GetPrecedence(operation))
                Reduce();

            operations.Push(operation);
            values.Push(ParseExpression(fields[index], defaultTable));
        }

        while (operations.Count > 0)
            Reduce();

        return values.Pop();

        void Reduce()
        {
            var right = values.Pop();
            var left = values.Pop();
            values.Push(new BinarySqlExpression(
                left,
                operations.Pop().ToSymbol(),
                right,
                typeof(decimal)));
        }
    }

    private static int GetPrecedence(MatchOperation operation)
        => operation is MatchOperation.Multiply or MatchOperation.Divide or MatchOperation.Modulus
            ? 2
            : 1;
}
