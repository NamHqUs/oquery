using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Utilities;
using OQuery.Execution;
using OQuery.QueryDefinitions.Parsers;
using OQuery.QueryBuilderDto.Parsers;
using System.Linq.Expressions;
using System.Reflection;

namespace OQuery.Compilation.Visitors;

class ClauseVisitor(QueryContext queryContext)
{
    private static readonly MethodInfo _queryMethod = typeof(IQueryParameter).GetMethod(nameof(IQueryParameter.Query))!;
    protected QueryContext QueryContext { get; } = queryContext;

    public SqlExpression Eval(MethodCallExpression methodCall)
    {
        var statements = new List<MethodCallExpression>() { methodCall };

        while (methodCall.Arguments[0] is MethodCallExpression method)
            statements.Insert(0, methodCall = method);

        if (statements.Any(statement => statement.Method.Name == nameof(FluentQueryExtensions.Recursive)))
            return EvalRecursive(statements);

        var tableContext = CreateTableContext(statements);

        foreach (var statement in statements)
            tableContext = ProcessStatement(tableContext, statement);

        return tableContext;
    }

    private TableExpression EvalRecursive(IReadOnlyList<MethodCallExpression> statements)
    {
        if (statements.Any(statement =>
                statement.Method.Name is not nameof(FluentQueryExtensions.Recursive)
                    and not nameof(FluentQueryExtensions.Select)
                    and not nameof(FluentQueryExtensions.ToList)))
            throw new NotSupportedException(
                "Recursive queries currently support only Recursive, Select, and ToList.");
        var recursiveStatement = statements.Single(statement =>
            statement.Method.Name == nameof(FluentQueryExtensions.Recursive));
        var selectStatement = statements.FirstOrDefault(statement =>
            statement.Method.Name == nameof(FluentQueryExtensions.Select));
        var combinedProjection = recursiveStatement.Arguments.Count == 3
            ? (LambdaExpression)recursiveStatement.Arguments[2].StripQuotes()
            : null;
        if (combinedProjection is null && selectStatement is null)
            throw new FormatException("Recursive queries require a projection.");
        if (combinedProjection is null &&
            (statements.Count < 2 ||
             !ReferenceEquals(statements[0], recursiveStatement) ||
             !ReferenceEquals(statements[1], selectStatement!)))
            throw new NotSupportedException(
                "Recursive queries require Query(entity).Recursive(relation).Select(dictionary).");
        if (recursiveStatement.Arguments[0] is not ConstantExpression { Value: DbQueryable source } ||
            recursiveStatement.Arguments[1] is not ConstantExpression { Value: string relationName })
            throw new NotSupportedException(
                "Recursive queries require Query(entity).Recursive(relation).Select(dictionary).");
        var selectionLambda = combinedProjection
            ?? (LambdaExpression)selectStatement!.Arguments[1].StripQuotes();
        var selection = ReadRecursiveSelection(selectionLambda, relationName);

        var sourceEntity = source.Provider.Entity;
        var relation = sourceEntity.GetRelation(relationName);
        if (relation.Type != RelationType.Collection ||
            !string.Equals(relation.RefEntity, sourceEntity.Name, StringComparison.OrdinalIgnoreCase))
            throw new FormatException(
                $"Recursive relation [{relationName}] must be a collection relation back to [{sourceEntity.Name}].");
        if (relation.RefKeys.Count != 1)
            throw new NotSupportedException(
                $"Recursive relation [{relationName}] must have exactly one key pair.");

        var key = relation.RefKeys[0];
        var contextName = $"{sourceEntity.Name}Tree";
        var projections = selection.Select(entry =>
        {
            var value = entry.Value as string
                ?? throw new NotSupportedException(
                    "Recursive projections must use string field expressions.");
            var field = value.StartsWith($"{relation.Name}.", StringComparison.OrdinalIgnoreCase)
                ? value[(relation.Name.Length + 1)..] switch
                {
                    var aggregate when aggregate.StartsWith("Count(", StringComparison.OrdinalIgnoreCase)
                        => $"CountOver(member.{aggregate[6..^1]}, manager.{key.DbColumn})",
                    var memberField => $"member.{memberField}"
                }
                : value;
            return new ProjectionDefinition(
                QueryDefinitionJsonParser.ParseExpression(field)[0],
                entry.Key);
        }).ToList();

        var definition = new QueryDefinition(sourceEntity.Name)
        {
            Projections = projections,
            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                [contextName] = new QueryDefinitionBase(sourceEntity.Name)
                {
                    Projections =
                    [
                        new ProjectionDefinition($"member.{key.DbColumn}", key.DbColumn)
                    ]
                }
            },
            Joins =
            [
                new JoinDefinition(
                    contextName,
                    "member",
                    QueryDefinitionJsonParser.ParseJoinCondition(
                        $"{key.DbColumn} = member.{key.RefDbColumn}"),
                    QueryJoinType.Inner)
            ]
        };

        return new QueryDefinitionVisitor(QueryContext).Eval(definition);
    }

    private static Dictionary<string, object?> ReadRecursiveSelection(
        LambdaExpression selection,
        string relationName)
    {
        if (selection.Parameters.Count == 1 &&
            selection.Compile().DynamicInvoke(new object?[] { null }) is Dictionary<string, object?> stringSelection)
            return stringSelection;

        if (selection.Parameters.Count != 2)
            throw new NotSupportedException(
                "Recursive projections require a string dictionary or a two-scope Select lambda.");

        var manager = selection.Parameters[0];
        var members = selection.Parameters[1];
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (selection.Body is NewExpression anonymousProjection &&
            anonymousProjection.Members is not null)
        {
            for (var index = 0; index < anonymousProjection.Members.Count; index++)
            {
                result[anonymousProjection.Members[index].Name] = ReadRecursiveValue(
                    anonymousProjection.Arguments[index],
                    manager,
                    members,
                    relationName);
            }
            return result;
        }

        if (selection.Body is not ListInitExpression list)
            throw new NotSupportedException(
                "Recursive projections require an anonymous object or string dictionary.");

        foreach (var initializer in list.Initializers)
        {
            if (initializer.Arguments.Count != 2 ||
                initializer.Arguments[0] is not ConstantExpression { Value: string alias })
                throw new FormatException("Recursive projection aliases must be constant strings.");

            result[alias] = ReadRecursiveValue(initializer.Arguments[1], manager, members, relationName);
        }

        return result;
    }

    private static string ReadRecursiveValue(
        Expression expression,
        ParameterExpression manager,
        ParameterExpression members,
        string relationName)
    {
        expression = expression.StripQuotes();
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } convert)
            expression = convert.Operand;

        if (expression is not MethodCallExpression method)
            throw new NotSupportedException(
                "Recursive projections support field access and member Count only.");

        if (method.Method.DeclaringType == typeof(QueryValue) &&
            method.Object is MethodCallExpression
            {
                Method.Name: "get_Item",
                Object: var aggregateScope,
                Arguments: [ConstantExpression { Value: string aggregateField }]
            })
        {
            if (aggregateScope == manager)
                throw new FormatException(
                    $"{method.Method.Name}() requires a collection scope.");
            if (aggregateScope == members)
                return $"{relationName}.{method.Method.Name}({aggregateField})";
        }

        if (method.Arguments.Count == 0 ||
            method.Arguments[0] is not ConstantExpression { Value: string field })
            throw new NotSupportedException(
                "Recursive projections support field access and member Count only.");

        if (method.Object == manager && method.Method.Name == "get_Item")
            return field;
        if (method.Object == members && method.Method.Name == "get_Item")
            return $"{relationName}.{field}";

        throw new NotSupportedException(
            "Recursive projections support field access and member Count only.");
    }

    private TableExpression CreateTableContext(List<MethodCallExpression> statements)
    {
        var methodCall = statements[0];
        var root = methodCall.Arguments[0];

        if (methodCall.Method == _queryMethod)
        {
            statements.Remove(methodCall);

            var relationPath = (string?)Expression.Lambda(root).Compile().DynamicInvoke();
            var tableContext = QueryContext.Arguments.Peek().TableContext;
            return QueryContext.JoinCollection(tableContext, relationPath);
        }

        if (root is QueryDefinitionExpression queryDefinition)
            return new QueryDefinitionVisitor(QueryContext).Eval(queryDefinition.Definition);

        var dbQuery = (DbQueryable)((ConstantExpression)root).Value!;
        return QueryContext.CreateTableContext(dbQuery.Provider.Entity);
    }

    private TableExpression ProcessStatement(TableExpression tableContext, MethodCallExpression methodCall)
    {
        switch (methodCall.Method.Name)
        {
            case nameof(FluentQueryExtensions.Select):
                return new SelectVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());

            case nameof(FluentQueryExtensions.Where):
                return new WhereVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());

            case nameof(FluentQueryExtensions.OrderBy):
            case nameof(FluentQueryExtensions.OrderByDescending):
            case nameof(FluentQueryExtensions.ThenBy):
            case nameof(FluentQueryExtensions.ThenByDescending):
            case nameof(FluentQueryExtensions.QuerySignatures.OrderByField):
            case nameof(FluentQueryExtensions.QuerySignatures.OrderByDescendingField):
            case nameof(FluentQueryExtensions.QuerySignatures.ThenByField):
            case nameof(FluentQueryExtensions.QuerySignatures.ThenByDescendingField):
                return ProcessOrdering(tableContext, methodCall);

            case nameof(FluentQueryExtensions.Any):
                tableContext = new WhereVisitor(tableContext, QueryContext).Eval((LambdaExpression)methodCall.Arguments[1].StripQuotes());
                return new AnyExpression(tableContext);

            case nameof(FluentQueryExtensions.Take):
                return new TakeExpression(tableContext, (int)((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!);

            case nameof(FluentQueryExtensions.Skip):
                return new SkipExpression(tableContext, (int)((ConstantExpression)methodCall.Arguments[1].StripQuotes()).Value!);

            case nameof(FluentQueryExtensions.GroupBy):
                foreach (var field in (string[])((ConstantExpression)methodCall.Arguments[1]).Value!)
                {
                    var expression = QueryContext.ParseQueryExpression(
                        tableContext,
                        FieldDefinitionParser.Parse(field));
                    if (expression.ContainsGroupedAggregate())
                        throw new FormatException("Group-by fields cannot contain aggregate expressions.");
                    tableContext.GroupByExpressions.Add(expression);
                }
                return tableContext;

            case nameof(FluentQueryExtensions.Count):
                if (methodCall.Arguments.Count != 2 ||
                    methodCall.Arguments[1] is not ConstantExpression { Value: string fieldName } ||
                    string.IsNullOrWhiteSpace(fieldName))
                    throw new UnauthorizedAccessException(
                        "COUNT(*) is not allowed. Specify an authorized field to count.");
                return QueryContext.CreateCountAggregate(tableContext, fieldName);

            case nameof(FluentQueryExtensions.ToList):
                return tableContext;

            default:
                throw new NotImplementedException(string.Format("not support method {0}.{1}",
                    methodCall.Method.DeclaringType?.Name??"<UnknownType>",
                    methodCall.Method.Name));
        }
    }

    private TableExpression ProcessOrdering(TableExpression tableContext, MethodCallExpression methodCall)
    {
        if (methodCall.Arguments[1] is ConstantExpression { Value: IFieldDefinition sortField })
        {
            var projectionAlias = sortField is FieldDefinition { Oper: MatchOperation.Add } fieldDefinition
                ? FindProjectionAlias(tableContext, fieldDefinition.FieldName)
                : null;
            tableContext.Orders.Add(new OrderExpression(
                QueryContext.ParseQueryExpression(tableContext, sortField),
                methodCall.Method.Name.EndsWith("DescendingField", StringComparison.Ordinal),
                projectionAlias));
            return tableContext;
        }

        var fields = (string[])((LambdaExpression)methodCall.Arguments[1].StripQuotes())
            .Compile()
            .DynamicInvoke(new object?[] { null })!;
        var descending = methodCall.Method.Name.EndsWith("Descending", StringComparison.Ordinal);

        foreach (var field in fields)
            tableContext.Orders.Add(new OrderExpression(
                QueryContext.ParseFieldPath(tableContext, field),
                descending,
                FindProjectionAlias(tableContext, field)));

        return tableContext;
    }

    private static string? FindProjectionAlias(TableExpression tableContext, string fieldPath)
        => tableContext.ProjectionItems
            .FirstOrDefault(item => string.Equals(item.Alias, fieldPath, StringComparison.OrdinalIgnoreCase))
            ?.Alias;
}
