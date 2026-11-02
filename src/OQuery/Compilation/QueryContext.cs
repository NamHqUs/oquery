using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.Contracts;
using OQuery.Contracts.QueryDefinitions.Parsers;
using OQuery.QueryBuilderDto.Parsers;
using static OQuery.SqlFunctionDefintion;

namespace OQuery.Compilation;

class QueryContext(
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect sqlDialect)
{
    private int _aliasCount;
    public readonly Stack<(string ArgumentName, TableExpression TableContext)> Arguments = new();
    private readonly List<DbParameter> _dbParameters = [];

    public IEnumerable<DbParameter> DbParameters => _dbParameters;
    public bool SupportsRecursiveCte => sqlDialect.SupportsRecursiveCte;

    public Entity GetEntity(string entityName)
        => metaProvider.GetEntity(entityName);

    public TableExpression CreateTableContext(Entity entity)
    {
        return new TableExpression(entity, GetNextAlias());
    }

    public SqlExpression ParseFieldPath(TableExpression tableContext, string fieldPath)
    {
        if (tableContext.ProjectionItems
            .FirstOrDefault(e => string.Equals(e.Alias, fieldPath, StringComparison.OrdinalIgnoreCase))
            ?.Item is SqlExpression projection)
            return projection;

        var separator = fieldPath.IndexOf('.');
        if (separator > 0)
        {
            var joinedTable = tableContext.JoinedTables
                .FirstOrDefault(pair => string.Equals(
                    pair.Key,
                    fieldPath[..separator],
                    StringComparison.OrdinalIgnoreCase))
                .Value;
            if (joinedTable is not null)
                return ParseFieldPath(joinedTable, fieldPath[(separator + 1)..]);
        }

        if (TryParseAggregateFunction(fieldPath, out var wrappedAggregate))
            return ParseFunctionExpression(tableContext, wrappedAggregate, 0);

        var i = FindLastUnnestedDot(fieldPath);
        var relationPath = i < 0 ? null : fieldPath[..i];
        var fieldName = fieldPath[(i + 1)..];

        var joinedData = ParseRelationPath(tableContext, relationPath);

        if (TryParseAggregateFunction(fieldName, out var aggregateFunction))
        {
            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            var function = (FunctionSqlExpression)ParseFunctionExpression(lastTable, aggregateFunction, 0);
            return new AggregateExpression(lastTable, function, !joinedData.Any());
        }

        var lastEntity = joinedData.LastOrDefault().joinedEntity ?? tableContext.Entity;
        var metaField = lastEntity.GetField(fieldName);
        var lastAlias = ApplyJoin(tableContext, joinedData).Alias;
        return new FieldExpression(lastAlias, metaField);
    }

    public SqlExpression ParseQueryExpression(TableExpression tableContext, IFieldDefinition expression, int depth = 0)
    {
        if (depth > 32)
            throw new FormatException("Query expressions cannot be nested more than 32 levels.");
        if (expression is null)
            throw new FormatException("A query expression cannot be null.");

        return expression switch
        {
            FieldDefinition field => ParseFieldExpression(tableContext, field),
            ConstFieldDefinition value => ParseValueExpression(value),
            GroupFieldDefintion group => ParseGroupExpression(tableContext, group, depth),
            FunctionFieldDefinition function => ParseFunctionExpression(tableContext, function, depth),
            _ => throw new FormatException($"Unsupported query expression [{expression.GetType().Name}].")
        };
    }

    public SqlExpression ParseQueryExpression(TableExpression tableContext, IFieldDefinition[] expressions, int depth = 0)
    {
        if (depth > 32)
            throw new FormatException("Query expressions cannot be nested more than 32 levels.");
        if (expressions is null || expressions.Length == 0)
            throw new FormatException("A query expression cannot be empty.");
        if (expressions.Any(expression => expression is null))
            throw new FormatException("A query expression cannot contain null fields.");
        if (expressions[0].Oper != MatchOperation.Add)
            throw new FormatException("The first field in a query expression must use the add operator.");

        var values = new Stack<SqlExpression>();
        var operations = new Stack<MatchOperation>();
        values.Push(ParseQueryExpression(tableContext, expressions[0], depth + 1));

        for (var index = 1; index < expressions.Length; index++)
        {
            var operation = expressions[index].Oper;
            if (!operation.IsArithmetic())
                throw new FormatException($"Unsupported grouped projection operator [{operation}].");

            while (operations.Count > 0 &&
                GetArithmeticPrecedence(operations.Peek()) >= GetArithmeticPrecedence(operation))
                Reduce();

            operations.Push(operation);
            values.Push(ParseQueryExpression(tableContext, expressions[index], depth + 1));
        }

        while (operations.Count > 0)
            Reduce();

        return values.Pop();

        void Reduce()
        {
            var right = values.Pop();
            var left = values.Pop();
            var currentOperation = operations.Pop();
            values.Push(new BinarySqlExpression(
                left,
                currentOperation.ToSymbol(),
                right,
                typeof(decimal)));
        }
    }

    private SqlExpression ParseFieldExpression(TableExpression tableContext, FieldDefinition expression)
        => string.IsNullOrWhiteSpace(expression.FieldName)
            ? throw new FormatException("A field query expression requires a field path.")
            : ParseFieldPath(tableContext, expression.FieldName);

    private SqlExpression ParseValueExpression(ConstFieldDefinition expression)
        => expression.Value switch
        {
            string value => new ValueExpression(AddDbParameter(value, true), typeof(string)),
            bool value => new ValueExpression(AddDbParameter(value, true), typeof(bool)),
            DateTime value => new ValueExpression(AddDbParameter(value, true), typeof(DateTime)),
            null => throw new FormatException("A value query expression requires a numeric value."),
            var value => new ValueExpression(AddDbParameter(ToDecimal(value), true), typeof(decimal))
        };

    private SqlExpression ParseGroupExpression(TableExpression tableContext, GroupFieldDefintion group, int depth)
    {
        var fields = group.Fields
            ?? throw new FormatException("A grouped query expression cannot have null fields.");
        if (fields.Length == 0)
            throw new FormatException("A grouped query expression cannot be empty.");
        return ParseQueryExpression(tableContext, fields, depth + 1);
    }

    private static int GetArithmeticPrecedence(MatchOperation operation)
        => operation switch
        {
            MatchOperation.Add or MatchOperation.Subtract => 1,
            MatchOperation.Multiply or MatchOperation.Divide or MatchOperation.Modulus => 2,
            _ => throw new FormatException($"Unsupported grouped projection operator [{operation}].")
        };

    private SqlExpression ParseFunctionExpression(TableExpression tableContext, FunctionFieldDefinition expression, int depth)
    {
        var definition = functionProvider.GetFunction(expression.FunctionName);
        var suppliedArguments = expression.Arguments
            ?? throw new FormatException("A function's arguments cannot be null.");
        var minimumArgumentCount = definition.Arguments.Count(argument => !argument.Optional);
        if (suppliedArguments.Length < minimumArgumentCount ||
            suppliedArguments.Length > definition.Arguments.Count)
            throw new FormatException($"Unsupported function or argument count [{expression.FunctionName}].");

        var arguments = new FunctionSqlArgument?[definition.Arguments.Count];
        for (var index = 0; index < definition.Arguments.Count; index++)
        {
            var argumentDefinition = definition.Arguments[index];
            arguments[index] = index < suppliedArguments.Length
                ? ParseFunctionArgument(tableContext, argumentDefinition, suppliedArguments[index], depth + 1)
                : ParseDefaultFunctionArgument(tableContext, argumentDefinition, depth + 1);
        }

        return new FunctionSqlExpression(definition, arguments);
    }

    private FunctionSqlArgument ParseFunctionArgument(
        TableExpression tableContext,
        SqlFunctionArgumentDefinition definition,
        IFieldDefinition expression,
        int depth)
    {
        if (expression is null)
            throw new FormatException($"Function argument [{definition.Name}] cannot be null.");

        if (expression is FieldDefinition { FieldName: "*" })
            return new FunctionSqlTokenArgument("*");

        if (expression is ConditionFieldDefinition condition)
            return new FunctionSqlExpressionArgument(
                ParsePredicateExpression(tableContext, condition.Condition, depth));

        if (definition.DataType == DataType.Boolean &&
            expression is FieldDefinition predicate &&
            LooksLikePredicate(predicate.FieldName))
        {
            var filter = ConditionDefinitionParser.Parse(predicate.FieldName)
                ?? throw new FormatException($"Function argument [{definition.Name}] requires a predicate.");
            return new FunctionSqlExpressionArgument(
                ParsePredicateExpression(tableContext, filter, depth));
        }

        if (definition.DataType == DataType.Datetime &&
            expression is ConstFieldDefinition { Value: string text })
        {
            if (!DateTime.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var dateTime))
                throw new FormatException($"Function argument [{definition.Name}] requires a valid date-time value.");

            expression = new ConstFieldDefinition(dateTime);
        }

        return new FunctionSqlExpressionArgument(ParseQueryExpression(tableContext, expression, depth));
    }

    private SqlExpression ParsePredicateExpression(
        TableExpression tableContext,
        IConditionDefintion filter,
        int depth)
    {
        if (filter is ConditionDefinition group)
        {
            SqlExpression? result = null;
            foreach (var child in group.Filters)
            {
                var expression = ParsePredicateExpression(tableContext, child, depth + 1);
                if (result is null)
                {
                    result = expression;
                    continue;
                }

                var logicalOperation = child is ConditionStatementDefinition { LogicalOperator: { } logicalOperator }
                    ? logicalOperator
                    : group.LogicalOperation;
                result = new BinarySqlExpression(
                    result,
                    logicalOperation.ToString().ToUpperInvariant(),
                    expression,
                    typeof(bool));
            }

            return result
                ?? throw new FormatException("A function predicate cannot be empty.");
        }

        if (filter is not ConditionStatementDefinition item)
            throw new FormatException($"Unsupported function predicate [{filter.GetType().Name}].");

        var left = ParseQueryExpression(tableContext, item.Left, depth + 1);
        if (item.Operator is BinaryOperator.IsNull or BinaryOperator.IsNotNull)
            return new PredicateExpression(
                left,
                item.Operator == BinaryOperator.IsNull ? "IS NULL" : "IS NOT NULL");

        var right = item.Right
            ?? throw new FormatException($"Predicate operator [{item.Operator}] requires a right operand.");
        if (item.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            if (right is not GroupFieldDefintion values || values.Fields.Length == 0)
                throw new FormatException("A function predicate set comparison requires values.");

            return new SetPredicateExpression(
                left,
                item.Operator == BinaryOperator.In ? "IN" : "NOT IN",
                values.Fields.Select(value => ParseQueryExpression(tableContext, value, depth + 1)).ToArray());
        }

        if (item.Operator is BinaryOperator.Between or BinaryOperator.NotBetween)
        {
            if (right is not GroupFieldDefintion { Fields.Length: 2 } range)
                throw new FormatException("A function predicate range comparison requires two values.");

            return new RangePredicateExpression(
                left,
                item.Operator == BinaryOperator.Between ? "BETWEEN" : "NOT BETWEEN",
                ParseQueryExpression(tableContext, range.Fields[0], depth + 1),
                ParseQueryExpression(tableContext, range.Fields[1], depth + 1));
        }

        if (item.Operator is BinaryOperator.Like or BinaryOperator.NotLike)
            return new PredicateExpression(
                left,
                item.Operator == BinaryOperator.Like ? "LIKE" : "NOT LIKE",
                ParseQueryExpression(tableContext, right, depth + 1));

        var operation = item.Operator switch
        {
            BinaryOperator.Equals => "=",
            BinaryOperator.NotEquals => "<>",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.LessThan => "<",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LessThanOrEqual => "<=",
            _ => throw new FormatException($"Unsupported function predicate operator [{item.Operator}].")
        };

        return new BinarySqlExpression(
            left,
            operation,
            ParseQueryExpression(tableContext, right, depth + 1),
            typeof(bool));
    }

    private static bool LooksLikePredicate(string expression)
        => expression.IndexOfAny(['=', '>', '<']) >= 0 ||
            expression.Contains(" LIKE ", StringComparison.OrdinalIgnoreCase) ||
            expression.Contains(" NOT LIKE ", StringComparison.OrdinalIgnoreCase) ||
            expression.Contains(" IN ", StringComparison.OrdinalIgnoreCase) ||
            expression.Contains(" NOT IN ", StringComparison.OrdinalIgnoreCase) ||
            expression.Contains(" IS ", StringComparison.OrdinalIgnoreCase);

    private static int FindLastUnnestedDot(string fieldPath)
    {
        var depth = 0;
        var lastDot = -1;
        for (var index = 0; index < fieldPath.Length; index++)
        {
            if (fieldPath[index] == '(')
                depth++;
            else if (fieldPath[index] == ')')
                depth--;
            else if (fieldPath[index] == '.' && depth == 0)
                lastDot = index;
        }

        return lastDot;
    }

    private FunctionSqlArgument? ParseDefaultFunctionArgument(
        TableExpression tableContext,
        SqlFunctionArgumentDefinition definition,
        int depth)
    {
        if (definition.DefaultValue is not { } defaultValue)
            return null;

        return defaultValue is "*"
            ? new FunctionSqlTokenArgument("*")
            : new FunctionSqlExpressionArgument(
                ParseQueryExpression(tableContext, new ConstFieldDefinition(defaultValue), depth));
    }

    private static decimal ToDecimal(object value)
    {
        if (value is string text)
        {
            if (text.EndsWith('m') || text.EndsWith('M'))
                text = text[..^1];
            if (decimal.TryParse(
                text,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
                return parsed;
            throw new FormatException("A projection value must be numeric.");
        }

        try
        {
            return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new FormatException("A projection value must be numeric.", exception);
        }
        catch (InvalidCastException exception)
        {
            throw new FormatException("A projection value must be numeric.", exception);
        }
    }

    private bool TryParseAggregateFunction(string fieldName, out FunctionFieldDefinition function)
    {
        function = null!;
        if (!fieldName.Contains('(') || !fieldName.EndsWith(')'))
            return false;

        if (FieldDefinitionParser.Parse(fieldName) is not FunctionFieldDefinition parsedFunction ||
            !functionProvider.TryGetFunction(parsedFunction.FunctionName, out var definition) ||
            definition is not { IsAggregate: true })
            return false;

        function = parsedFunction;
        return true;
    }

    public TableExpression JoinCollection(TableExpression tableContext, string? relationPath)
    {
        var joinedData = ParseRelationPath(tableContext, relationPath);
        var lastTable = ApplyJoin(tableContext, joinedData.Take(joinedData.Count() - 1));

        var (relation, entity) = joinedData.Last();
        var joinedTable = new TableExpression(entity, GetNextAlias());
        joinedTable.SqlWhere.AddRange(BuildJoinKeys(lastTable, joinedTable, relation));

        return joinedTable;
    }

    private IEnumerable<(Relation relation, Entity joinedEntity)> ParseRelationPath(TableExpression table, string? relationPath)
    {
        var next = table.Entity;

        return string.IsNullOrEmpty(relationPath)
            ? Enumerable.Empty<(Relation relation, Entity joinedEntity)>()
            : relationPath.Split('.')
                .Select(e =>
                {
                    var rel = next.GetRelation(e);
                    next = metaProvider.GetEntity(rel.RefEntity);
                    return (rel, next);
                })
                .ToList();
    }

    private TableExpression ApplyJoin(TableExpression tableContext, IEnumerable<(Relation relation, Entity joinedEntity)> joinedData)
    {
        var lastTable = tableContext;
        foreach (var (relation, joinedEntity) in joinedData)
        {
            if (!lastTable.JoinedTables.TryGetValue(relation.Name, out TableExpression? joinedTable))
            {
                lastTable.JoinedTables[relation.Name] = joinedTable = new TableExpression(joinedEntity, GetNextAlias())
                {
                    JoinRoot = tableContext.JoinHost
                };
                tableContext.JoinHost.Joins.Add(BuildJoinPath(lastTable, joinedTable, relation));
            }
            lastTable = joinedTable;
        }

        return lastTable;
    }

    private TableExpression ApplyJoinAggregate(TableExpression tableContext, IEnumerable<(Relation relation, Entity joinedEntity)> joinedData)
    {
        var lastTable = tableContext;
        if (joinedData.Any())
        {
            List<SqlExpression>? predicates = null;
            foreach (var (relation, joinedEntity) in joinedData)
            {
                var joinedTable = new TableExpression(joinedEntity, GetNextAlias());
                if (predicates == null)
                    predicates = BuildJoinKeys(tableContext, joinedTable, relation);
                else
                {
                    joinedTable.Joins.Add(BuildJoinPath(lastTable, joinedTable, relation, true));
                    joinedTable.Joins.AddRange(lastTable.Joins);
                }
                lastTable = joinedTable;
            }

            if (predicates is not null)
                lastTable.SqlWhere.AddRange(predicates);
        }

        return lastTable;
    }

    private JoinExpression BuildJoinPath(TableExpression table, TableExpression joinedTable, Relation relation, bool reverse = false)
    {
        var joinKeys = BuildJoinKeys(table, joinedTable, relation);
        //var joinOper = relation.Type == RelationType.Collection ? "INNER JOIN" : "LEFT JOIN";
        var joinOper = "LEFT JOIN";

        joinedTable = reverse ? table : joinedTable;
        return new JoinExpression(reverse ? table : joinedTable, joinOper, joinKeys);
    }

    private List<SqlExpression> BuildJoinKeys(TableExpression table, TableExpression joinedTable, Relation relation)
    {
        var predicates = new List<SqlExpression>();
        foreach (var e in relation.RefKeys)
            predicates.Add(new BinarySqlExpression(
                new FieldExpression(joinedTable.Alias, new Field(e.RefDbColumn, e.RefDbColumn)),
                "=",
                new FieldExpression(table.Alias, new Field(e.DbColumn, e.DbColumn)),
                typeof(bool)));

        return predicates;
    }

    public string AddDbParameter(object? value, bool forceParameter = false)
    {
        if (!forceParameter &&
            (value is byte || value is short || value is int || value is long ||
             value is float || value is decimal || value is double))
            return value.ToString()!;

        if (value is bool b)
        {
            var boolName = string.Format("@p{0}", _dbParameters.Count);
            _dbParameters.Add(new DbParameter(boolName, sqlDialect.NormalizeParameterValue(b)));
            return boolName;
        }

        if (!forceParameter && value is string st && !st.Contains('\''))
            return $"'{st}'";

        var name = string.Format("@p{0}", _dbParameters.Count);
        _dbParameters.Add(new DbParameter(name, sqlDialect.NormalizeParameterValue(value)));

        return name;
    }
    public string GetNextAlias() => "t" + _aliasCount++;
}
