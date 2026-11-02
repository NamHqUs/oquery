using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.QueryDefinitions.Parsers;
using System.Linq.Expressions;
using System.Text.Json;

namespace OQuery.Execution;

internal static class DbQueryFromQueryDto
{
    public static IOQuery BuildQueryable(OQueryEngine context, string yml)
        => BuildQueryable(context, QueryDefinitionParser.ParseFromYml(yml));
    public static IOQuery BuildQueryable(OQueryEngine context, QueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Projection);

        if (definition.Contexts is { Count: > 0 })
        {
            var sourceEntity = GetProviderEntityName(definition, definition.Entity);
            var source = context.Query(sourceEntity);
            return source.Provider.CreateQuery(new QueryDefinitionExpression(definition));
        }

        var query = context.Query(definition.Entity);

        if (definition.Filter is not null)
            query = query.Where(CreateFilter(definition.Filter));

        if (definition.Projection.Count > 0)
        {
            var selection = new Dictionary<string, object?>();
            foreach (var field in definition.Projection)
            {
                var alias = string.IsNullOrWhiteSpace(field.Alias)
                    ? field.ToText()
                    : field.Alias;
                selection[alias] = field.Fields;
            }

            query = query.Select(selection);
        }

        foreach (var sortGroup in definition.Sorts.Select((sort, index) => (sort, index)))
        {
            query = sortGroup.index == 0
                ? sortGroup.sort.Direction == QuerySortDirection.Ascending
                    ? query.OrderByField(sortGroup.sort.Field)
                    : query.OrderByDescendingField(sortGroup.sort.Field)
                : sortGroup.sort.Direction == QuerySortDirection.Ascending
                    ? query.ThenByField(sortGroup.sort.Field)
                    : query.ThenByDescendingField(sortGroup.sort.Field);
        }

        if (definition.PageSize is < 0)
            throw new ArgumentOutOfRangeException(nameof(definition.PageSize), "Page size cannot be negative.");

        if (definition.PageSize is > 0)
        {
            var page = definition.Page ?? 1;
            var pageSize = definition.PageSize.Value;

            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(definition.Page), "Page must be at least 1.");

            query = query.Skip((page - 1) * pageSize);
            query = query.TakeQuery(pageSize);
        }

        return query;
    }

    private static string GetProviderEntityName(QueryDefinition definition, string entityName)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var context = definition.Contexts?.FirstOrDefault(queryContext =>
                string.Equals(queryContext.Name, entityName, StringComparison.OrdinalIgnoreCase));
            if (context is null)
                return entityName;
            if (!visited.Add(context.Name))
                throw new FormatException($"Query context source cycle detected at [{context.Name}].");
            entityName = context.Query.Entity;
        }
    }

    internal static Expression<Func<IQueryParameter, bool>> CreateFilter(ConditionDefinition filter)
    {
        var parameter = Expression.Parameter(typeof(IQueryParameter), "record");
        var body = CreateFilterBody(filter, parameter);

        return Expression.Lambda<Func<IQueryParameter, bool>>(body, parameter);
    }

    private static Expression CreateFilterBody(
        IConditionDefintion filter,
        ParameterExpression parameter)
        => filter switch
        {
            ConditionDefinition group => CreateLogicalFilter(group, parameter),
            ConditionStatementDefinition binary => CreateCondition(binary, parameter),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

    private static Expression CreateLogicalFilter(
        ConditionDefinition group,
        ParameterExpression parameter)
    {
        if (group.Filters.Count == 0)
            return Expression.Constant(true);

        var expression = CreateFilterBody(group.Filters[0], parameter);
        foreach (var filter in group.Filters.Skip(1))
        {
            var next = CreateFilterBody(filter, parameter);
            var logicalOperation = filter is ConditionStatementDefinition { LogicalOperator: not null } binary
                ? binary.LogicalOperator.Value
                : group.LogicalOperation;
            expression = logicalOperation == LogicalOperator.And
                ? Expression.AndAlso(expression, next)
                : Expression.OrElse(expression, next);
        }

        return expression;
    }

    private static Expression CreateCondition(
        ConditionStatementDefinition filter,
        ParameterExpression parameter)
    {
        var field = CreateFieldAccess(filter.Left, parameter);
        var value = filter.Operator is BinaryOperator.Between or BinaryOperator.NotBetween or BinaryOperator.In or BinaryOperator.NotIn
            ? null
            : CreateFilterValue(filter, parameter);

        if (filter.OperatorId is not null)
        {
            if (string.IsNullOrWhiteSpace(filter.CustomSqlTemplate))
                throw new FormatException($"Custom operator '{filter.OperatorId}' has no trusted SQL template.");
            return Expression.Call(
                field,
                typeof(QueryValue).GetMethod(nameof(QueryValue.CompareTemplate))!,
                value!,
                Expression.Constant(filter.CustomSqlTemplate));
        }

        if (filter.Operator == BinaryOperator.Like)
        {
            if (field.Type != typeof(QueryValue))
                throw new FormatException("The left side of a LIKE filter must be a field reference.");

            return Expression.Call(
                field,
                typeof(QueryValue).GetMethod(nameof(QueryValue.Like))!,
                value!);
        }

        return filter.Operator switch
        {
            BinaryOperator.Equals => Expression.Equal(field, value!),
            BinaryOperator.NotEquals => Expression.NotEqual(field, value!),
            BinaryOperator.GreaterThan => Expression.GreaterThan(field, value!),
            BinaryOperator.LessThan => Expression.LessThan(field, value!),
            BinaryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(field, value!),
            BinaryOperator.LessThanOrEqual => Expression.LessThanOrEqual(field, value!),
            BinaryOperator.NotLike => Expression.Call(
                field,
                typeof(QueryValue).GetMethod(nameof(QueryValue.NotLike))!,
                value!),
            BinaryOperator.IsNull => Expression.Call(
                field,
                typeof(QueryValue).GetMethod(nameof(QueryValue.IsNull))!),
            BinaryOperator.IsNotNull => Expression.Call(
                field,
                typeof(QueryValue).GetMethod(nameof(QueryValue.IsNotNull))!),
            BinaryOperator.Between or BinaryOperator.NotBetween => CreateRangeCondition(filter, field, parameter),
            BinaryOperator.In or BinaryOperator.NotIn => CreateSetCondition(filter, field, parameter),
            _ => throw new ArgumentOutOfRangeException(nameof(filter.Operator))
        };
    }

    private static Expression CreateRangeCondition(
        ConditionStatementDefinition filter,
        Expression field,
        ParameterExpression parameter)
    {
        var values = GetMultipleFilterValues(filter, parameter, 2);
        return Expression.Call(
            field,
            typeof(QueryValue).GetMethod(
                filter.Operator == BinaryOperator.Between
                    ? nameof(QueryValue.Between)
                    : nameof(QueryValue.NotBetween))!,
            values[0],
            values[1]);
    }

    private static Expression CreateSetCondition(
        ConditionStatementDefinition filter,
        Expression field,
        ParameterExpression parameter)
    {
        var values = GetMultipleFilterValues(filter, parameter, null);
        var array = Expression.NewArrayInit(typeof(object), values);
        return Expression.Call(
            field,
            typeof(QueryValue).GetMethod(
                filter.Operator == BinaryOperator.In
                    ? nameof(QueryValue.In)
                    : nameof(QueryValue.NotIn))!,
            array);
    }

    private static Expression[] GetMultipleFilterValues(
        ConditionStatementDefinition filter,
        ParameterExpression parameter,
        int? expectedCount)
    {
        var fields = filter.Right switch
        {
            GroupFieldDefintion group => group.Fields,
            ConstFieldDefinition { Value: string value } when
                filter.Operator is BinaryOperator.Between or BinaryOperator.NotBetween or BinaryOperator.In or BinaryOperator.NotIn
                => value
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => (IFieldDefinition)new ConstFieldDefinition(item))
                    .ToArray(),
            null => Array.Empty<IFieldDefinition>(),
            _ => new[] { filter.Right }
        };
        if (fields.Length == 0 || expectedCount is not null && fields.Length != expectedCount)
            throw new FormatException(
                expectedCount is null
                    ? "Set comparison values cannot be empty."
                    : $"The {filter.Operator} operator requires exactly {expectedCount} values.");

        return fields
            .Select(field => CreateFilterValue(
                new ConditionStatementDefinition(null, filter.Left, filter.Operator, field)
                {
                    Parameterized = filter.Parameterized
                },
                parameter))
            .Select(value => value.Type == typeof(object)
                ? value
                : Expression.Convert(value, typeof(object)))
            .ToArray();
    }

    private static string GetFilterFieldName(IFieldDefinition field)
        => field is FieldDefinition fieldDto
            ? fieldDto.FieldName
            : throw new FormatException(
                $"Filter fields must be field references. Received [{field.GetType().Name}].");

    private static Expression CreateFieldAccess(IFieldDefinition field, ParameterExpression parameter)
        => field switch
        {
            FieldDefinition fieldDto => Expression.Call(
                parameter,
                typeof(IQueryParameter).GetProperty("Item")!.GetMethod!,
                Expression.Constant(GetFilterFieldName(fieldDto))),
            FunctionFieldDefinition or GroupFieldDefintion
                => Expression.Call(
                    typeof(QueryValue).GetMethod(nameof(QueryValue.QueryExpression))!,
                    Expression.Constant(field, typeof(IFieldDefinition))),
            _ => throw new FormatException(
                $"Filter field expressions must be field references or query expressions. Received [{field.GetType().Name}].")
        };

    private static Expression CreateFilterValue(
        ConditionStatementDefinition filter,
        ParameterExpression parameter)
    {
        return filter.Right switch
        {
            null => Expression.Constant(null, typeof(object)),
            FieldDefinition field => CreateFieldAccess(field, parameter),
            FunctionFieldDefinition or GroupFieldDefintion
                => CreateFieldAccess(filter.Right, parameter),
            ConstFieldDefinition constant => CreateFilterConstant(constant.Value, filter.Parameterized),
            _ => throw new FormatException(
                $"Filter right expressions must be field references or constants. Received [{filter.Right.GetType().Name}].")
        };
    }

    private static Expression CreateFilterConstant(object? value, bool parameterized)
    {
        var normalizedValue = NormalizeFilterValue(value);
        return parameterized
            ? Expression.Call(
                typeof(QueryValue).GetMethod(nameof(QueryValue.Parameter))!,
                Expression.Constant(normalizedValue, typeof(object)))
            : Expression.Constant(normalizedValue, typeof(object));
    }

    private static object? NormalizeFilterValue(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            _ => throw new FormatException($"Unsupported JSON filter value [{element.ValueKind}].")
        };
    }
}
