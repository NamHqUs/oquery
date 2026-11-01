using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.Execution;

namespace OQuery.Compilation.Visitors;

internal sealed class QueryDefinitionVisitor(QueryContext queryContext)
{
    public TableExpression Eval(QueryDefinition definition)
    {
        if (definition.Contexts is not { Count: > 0 })
            throw new FormatException("A query-definition expression requires at least one context.");

        if (definition.Contexts.Any(context => context.Query.Contexts is { Count: > 0 }))
            throw new NotSupportedException("Nested query contexts are not supported.");

        var definitions = new Dictionary<string, QueryContextDefintion>(StringComparer.OrdinalIgnoreCase);
        List<QueryContextDefintion> cteDefinitions = [];
        HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (var context in definition.Contexts)
        {
            if (string.IsNullOrWhiteSpace(context.Name))
                throw new FormatException("A query context requires a name.");
            if (!aliases.Add(GetContextAlias(context)))
                throw new FormatException($"Duplicate query context alias [{GetContextAlias(context)}].");
            if (definitions.TryGetValue(context.Name, out var existing))
            {
                if (!string.Equals(existing.Query.ToYml(), context.Query.ToYml(), StringComparison.Ordinal))
                    throw new FormatException(
                        $"Query context [{context.Name}] has conflicting query definitions.");
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
                continue;
            if (dependencyIndex == index)
                continue;
            if (dependencyIndex > index)
                throw new FormatException(
                    $"Query context [{context.Name}] references [{context.Query.Entity}], which must be declared first.");
        }

        if (TryBuildRecursiveQuery(definition, definitions, cteDefinitions, out var recursiveQuery))
            return recursiveQuery;

        var table = CreateSource(definition.Entity, definitions);
        var cteTables = new Dictionary<string, TableExpression>(StringComparer.OrdinalIgnoreCase);
        foreach (var context in definition.Contexts)
        {
            var alias = GetContextAlias(context);
            var cteTable = table is CteTableExpression rootCte &&
                string.Equals(rootCte.Name, context.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(alias, context.Name, StringComparison.OrdinalIgnoreCase)
                    ? table
                    : new CteTableExpression(
                        context.Name,
                        CreateCteEntity(definitions[context.Name], definitions),
                        queryContext.GetNextAlias());
            cteTables.Add(alias, cteTable);
            table.JoinedTables.Add(alias, cteTable);
        }

        foreach (var context in definition.Contexts.Where(context => context.Condition is not null))
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
            foreach (var reference in definition.Contexts.Where(reference =>
                string.Equals(reference.Name, context.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var cteTable = cteTables[GetContextAlias(reference)];
                cteTable.Entity.Fields.Clear();
                cteTable.Entity.Fields.AddRange(cteQuery.ProjectionItems.Select(item => new Field(item.Alias, item.Alias)));
            }
        }

        return BuildQueryBody(definition, table, definitions);
    }

    private bool TryBuildRecursiveQuery(
        QueryDefinition definition,
        IReadOnlyDictionary<string, QueryContextDefintion> definitions,
        IReadOnlyList<QueryContextDefintion> contexts,
        out TableExpression query)
    {
        query = null!;
        var recursiveContexts = contexts
            .Where(context =>
                context.Recursive ||
                ContainsContextReference(context.Query, context.Name))
            .ToArray();
        if (recursiveContexts.Length == 0)
            return false;
        if (recursiveContexts.Length != 1 || contexts.Count != 1)
            throw new NotSupportedException("Only one recursive query context is supported.");
        if (!queryContext.SupportsRecursiveCte)
            throw new NotSupportedException("The selected SQL dialect does not support recursive CTEs.");

        var context = recursiveContexts[0];
        if (context.Recursive &&
            !ContainsContextReference(context.Query, context.Name) &&
            (context.Condition is null ||
                !ContainsContextReference(context.Condition, context.Name)))
            throw new FormatException(
                $"Recursive query context [{context.Name}] must reference itself.");

        var anchorDefinition = new QueryDefinition
        {
            Entity = definition.Entity,
            Projection = definition.Projection,
            Filter = definition.Filter,
            Sorts = definition.Sorts,
            Page = null,
            PageSize = null
        };
        var anchor = BuildQuery(anchorDefinition, definitions);
        var cteEntity = CreateCteEntity(context, definitions);
        var recursiveCte = new CteTableExpression(
            context.Name,
            cteEntity,
            queryContext.GetNextAlias());
        var recursive = CreateSource(context.Query.Entity, definitions);
        recursive.JoinedTables[GetContextAlias(context)] = recursiveCte;
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
            cteEntity.Fields);
        var result = new CteTableExpression(
            context.Name,
            cteEntity,
            queryContext.GetNextAlias());
        result.CommonTableExpressions.Add(new RecursiveCommonTableExpression(
            context.Name,
            anchor,
            recursive,
            cteEntity.Fields.Select(field => field.Name).ToArray()));
        result.ProjectionItems = cteEntity.Fields
            .Select(field => new ProjectionItem(
                new FieldExpression(result.Alias, field),
                field.Name))
            .ToList();
        query = result;
        return true;
    }

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

    private static bool ContainsContextReference(QueryDefinition definition, string contextName)
    {
        return definition.Projection.Any(projection =>
                projection.Fields.Any(field => ContainsContextReference(field, contextName))) ||
            (definition.Filter is not null && ContainsContextReference(definition.Filter, contextName)) ||
            definition.Sorts.Any(sort => ContainsContextReference(sort.Field, contextName));
    }

    private static bool ContainsContextReference(IFieldDefinition field, string contextName)
        => field switch
        {
            FieldDefinition value => IsContextField(value.FieldName, contextName),
            FunctionFieldDefinition function => function.Arguments
                .Any(argument => ContainsContextReference(argument, contextName)),
            GroupFieldDefintion group => group.Fields
                .Any(value => ContainsContextReference(value, contextName)),
            ConditionFieldDefinition condition => ContainsContextReference(condition.Condition, contextName),
            _ => false
        };

    private static bool ContainsContextReference(
        IConditionDefintion condition,
        string contextName)
        => condition switch
        {
            ConditionDefinition group => group.Filters
                .Any(filter => ContainsContextReference(filter, contextName)),
            ConditionStatementDefinition statement =>
                ContainsContextReference(statement.Left, contextName) ||
                (statement.Right is not null &&
                    ContainsContextReference(statement.Right, contextName)),
            _ => false
        };

    private static bool IsContextField(string fieldName, string contextName)
    {
        var separator = fieldName.IndexOf('.');
        return separator > 0 &&
            string.Equals(fieldName[..separator], contextName, StringComparison.OrdinalIgnoreCase);
    }

    private TableExpression CreateSource(
        string entityName,
        IReadOnlyDictionary<string, QueryContextDefintion> definitions)
    {
        if (definitions.TryGetValue(entityName, out var context))
            return new CteTableExpression(
                context.Name,
                CreateCteEntity(context, definitions),
                queryContext.GetNextAlias());

        return queryContext.CreateTableContext(queryContext.GetEntity(entityName));
    }

    private Entity CreateCteEntity(
        QueryContextDefintion context,
        IReadOnlyDictionary<string, QueryContextDefintion> definitions)
    {
        var fields = GetCteFields(context, definitions, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return new Entity(context.Name, fields, DbTable: context.Name);
    }

    private List<Field> GetCteFields(
        QueryContextDefintion context,
        IReadOnlyDictionary<string, QueryContextDefintion> definitions,
        HashSet<string> visited)
    {
        if (!visited.Add(context.Name))
            throw new FormatException($"Query context dependency cycle detected at [{context.Name}].");
        var projections = context.Query.Projection.Where(projection => !projection.IsNewLine).ToArray();
        if (projections.Length > 0)
            return projections.Select(projection =>
            {
                var name = GetProjectionAlias(projection);
                return new Field(name, name);
            }).ToList();
        if (definitions.TryGetValue(context.Query.Entity, out var sourceContext))
            return GetCteFields(sourceContext, definitions, visited);

        return queryContext.GetEntity(context.Query.Entity).Fields
            .Select(field => new Field(field.Name, field.Name))
            .ToList();
    }

    private TableExpression BuildQuery(
        QueryDefinition definition,
        IReadOnlyDictionary<string, QueryContextDefintion> definitions)
    {
        var table = CreateSource(definition.Entity, definitions);
        return BuildQueryBody(definition, table, definitions);
    }

    private TableExpression BuildQueryBody(
        QueryDefinition definition,
        TableExpression table,
        IReadOnlyDictionary<string, QueryContextDefintion> definitions)
    {
        if (definition.Filter is not null)
            table = new WhereVisitor(table, queryContext)
                .Eval(DbQueryFromQueryDto.CreateFilter(definition.Filter));

        var projections = definition.Projection.Where(projection => !projection.IsNewLine).ToArray();
        if (projections.Length > 0)
        {
            table.ProjectionItems = projections
                .Select(projection => new ProjectionItem(
                    queryContext.ParseQueryExpression(table, projection.Fields),
                    GetProjectionAlias(projection)))
                .ToList();
        }
        else
        {
            table.EnsureDefaultProjection();
        }

        foreach (var sort in definition.Sorts)
            table.Orders.Add(new OrderExpression(
                queryContext.ParseQueryExpression(table, sort.Field),
                sort.Direction == QuerySortDirection.Descending));

        if (definition.PageSize is < 0)
            throw new ArgumentOutOfRangeException(nameof(definition.PageSize), "Page size cannot be negative.");

        if (definition.PageSize is > 0)
        {
            var page = definition.Page ?? 1;
            var pageSize = definition.PageSize.Value;
            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(definition.Page), "Page must be at least 1.");

            table = new SkipExpression(table, (page - 1) * pageSize);
            table = new TakeExpression(table, pageSize);
        }

        return table;
    }

    private static string GetProjectionAlias(ProjectionDefinition projection)
        => string.IsNullOrWhiteSpace(projection.Alias)
            ? string.Join(" ", projection.Fields.Select(field => field.ToText()))
            : projection.Alias;

    private static string GetContextAlias(QueryContextDefintion context)
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
