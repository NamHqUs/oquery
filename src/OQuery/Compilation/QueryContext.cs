using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.QueryDefinitions.Parsers;
using OQuery.QueryBuilderDto.Parsers;
using static OQuery.SqlFunctionDefintion;

namespace OQuery.Compilation;

class QueryContext(
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect sqlDialect)
{
    private int _aliasCount;
    private bool _parseContextAggregatesAsScalar;
    public readonly Stack<(string ArgumentName, TableExpression TableContext)> Arguments = new();
    private readonly List<DbParameter> _dbParameters = [];

    public IEnumerable<DbParameter> DbParameters => _dbParameters;
    public bool SupportsRecursiveCte => sqlDialect.SupportsRecursiveCte;

    public IEntity GetEntity(string entityName)
        => metaProvider.GetEntity(entityName);

    public IReadOnlyList<Field> GetFields(IEntity entity)
        => metaProvider.GetFields(entity);

    public IReadOnlyList<Field> GetFields(TableExpression table)
        => table.AuthorizedFields;

    public TableExpression ResolveTableContext(TableExpression tableContext, string relationPath)
    {
        if (tableContext.JoinedTables.TryGetValue(relationPath, out var joinedTable))
            return joinedTable;

        var joinedData = ParseRelationPath(tableContext, relationPath).ToArray();
        return ApplyJoin(tableContext, joinedData);
    }

    public AggregateExpression CreateCountAggregate(TableExpression tableContext, string fieldName)
    {
        if (string.Equals(fieldName, "*", StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                "COUNT(*) is not allowed. Specify an authorized field to count.");

        var field = ParseFieldPath(tableContext, fieldName);
        var definition = functionProvider.GetFunction("Count");
        return new AggregateExpression(
            tableContext,
            new FunctionSqlExpression(
                definition,
                [new FunctionSqlExpressionArgument(field)]));
    }

    public TableExpression CreateTableContext(IEntity entity)
    {
        return new TableExpression(entity, GetNextAlias(), metaProvider.GetFields(entity));
    }

    public TableExpression CreateTableContext(IEntity entity, string alias)
    {
        return new TableExpression(entity, alias, metaProvider.GetFields(entity));
    }

    public SqlExpression ParseFieldPath(TableExpression tableContext, string fieldPath)
    {
        if (tableContext.ProjectionItems
            .FirstOrDefault(e => string.Equals(e.Alias, fieldPath, StringComparison.OrdinalIgnoreCase))
            is { Alias: var alias, Item: var projection })
            return new ProjectionAliasExpression(alias, projection.Type);
        if (tableContext.PendingProjectionAliases.TryGetValue(fieldPath, out var pendingType))
            return new ProjectionAliasExpression(fieldPath, pendingType);

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

        if (TryParseFunction(fieldPath, out var wrappedFunction))
            return ParseFunctionExpression(tableContext, wrappedFunction, 0);

        var i = FindLastUnnestedDot(fieldPath);
        var relationPath = i < 0 ? null : fieldPath[..i];
        var fieldName = fieldPath[(i + 1)..];

        var joinedData = ParseRelationPath(tableContext, relationPath).ToArray();

        if (TryParseAggregateFunction(fieldName, out var aggregateFunction))
        {
            if (TryParseRelationAggregate(tableContext, relationPath, aggregateFunction, 0, out var relationAggregate))
                return relationAggregate;

            var lastTable = ApplyJoinAggregate(tableContext, joinedData);
            var function = (FunctionSqlExpression)ParseFunctionExpression(lastTable, aggregateFunction, 0);
            return new AggregateExpression(lastTable, function, !joinedData.Any());
        }

        var lastEntity = joinedData.LastOrDefault().joinedEntity ?? tableContext.Entity;
        var metaField = metaProvider.GetField(lastEntity, fieldName);
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

    public SqlExpression ParseScalarQueryExpression(TableExpression tableContext, IFieldDefinition expression)
    {
        var previous = _parseContextAggregatesAsScalar;
        _parseContextAggregatesAsScalar = true;
        try
        {
            return ParseQueryExpression(tableContext, expression);
        }
        finally
        {
            _parseContextAggregatesAsScalar = previous;
        }
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
            var stringConcat = currentOperation == MatchOperation.Add
                && (left.Type == typeof(string) || right.Type == typeof(string));
            values.Push(new BinarySqlExpression(
                left,
                currentOperation.ToSymbol(),
                right,
                stringConcat ? typeof(string) : typeof(decimal),
                stringConcat));
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
        if (TryParseCollectionAggregate(tableContext, expression, depth, out var collectionAggregate))
            return collectionAggregate;

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

        if (string.Equals(definition.Name, "Count", StringComparison.OrdinalIgnoreCase) &&
            arguments.Any(argument => argument is FunctionSqlTokenArgument { Token: "*" }))
        {
            throw new UnauthorizedAccessException(
                "COUNT(*) is not allowed. Specify an authorized field to count.");
        }

        if (_parseContextAggregatesAsScalar &&
            TryParseContextAggregate(tableContext, expression, definition, depth, out var contextAggregate))
            return contextAggregate;

        return new FunctionSqlExpression(definition, arguments);
    }

    private bool TryParseCollectionAggregate(
        TableExpression tableContext,
        FunctionFieldDefinition expression,
        int depth,
        out AggregateExpression aggregate)
    {
        aggregate = null!;
        var separator = expression.FunctionName.LastIndexOf('.');
        if (separator <= 0)
            return false;

        var relationPath = expression.FunctionName[..separator];
        var functionName = expression.FunctionName[(separator + 1)..];
        if (!functionProvider.TryGetFunction(functionName, out var definition) ||
            definition is not { IsAggregate: true })
            return false;

        var aggregateDefinition = expression with { FunctionName = functionName };
        return TryParseRelationAggregate(tableContext, relationPath, aggregateDefinition, depth, out aggregate);
    }

    private bool TryParseRelationAggregate(
        TableExpression tableContext,
        string? relationPath,
        FunctionFieldDefinition aggregateDefinition,
        int depth,
        out AggregateExpression aggregate)
    {
        aggregate = null!;
        if (string.IsNullOrWhiteSpace(relationPath))
            return false;

        var separator = relationPath.IndexOf('.');
        if (separator > 0 &&
            tableContext.JoinedTables.TryGetValue(relationPath[..separator], out var joinedTable))
        {
            return TryParseRelationAggregate(
                joinedTable,
                relationPath[(separator + 1)..],
                aggregateDefinition,
                depth,
                out aggregate);
        }

        var joinedData = ParseRelationPath(tableContext, relationPath).ToArray();
        if (joinedData is [{ relation: var relation, joinedEntity: var joinedEntity }] &&
            relation.Type == RelationType.Collection &&
            tableContext is not CteTableExpression)
        {
            aggregate = ApplyGroupedJoinAggregate(
                tableContext,
                relation,
                joinedEntity,
                aggregateDefinition);
            return true;
        }

        var lastTable = ApplyJoinAggregate(tableContext, joinedData);
        var function = ParseFunctionExpression(lastTable, aggregateDefinition, depth + 1) as FunctionSqlExpression
            ?? throw new InvalidOperationException(
                $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
        aggregate = new AggregateExpression(lastTable, function, !joinedData.Any());
        return true;
    }

    private bool TryParseContextAggregate(
        TableExpression tableContext,
        FunctionFieldDefinition expression,
        SqlFunctionDefintion definition,
        int depth,
        out AggregateExpression aggregate)
    {
        aggregate = null!;
        if (!definition.IsAggregate ||
            expression.Arguments.Length != 1 ||
            expression.Arguments[0] is not FieldDefinition field)
            return false;

        var separator = field.FieldName.IndexOf('.');
        if (separator <= 0 ||
            !tableContext.JoinedTables.TryGetValue(field.FieldName[..separator], out var contextTable) ||
            contextTable is not CteTableExpression)
            return false;

        var contextField = field.FieldName[(separator + 1)..];
        if (string.IsNullOrWhiteSpace(contextField))
            return false;

        var contextFunction = expression with
        {
            Arguments = [new FieldDefinition(contextField)]
        };
        var function = ParseFunctionExpression(contextTable, contextFunction, depth + 1);
        if (function is not FunctionSqlExpression aggregateFunction)
            throw new InvalidOperationException(
                $"Aggregate function [{expression.FunctionName}] did not produce a SQL function.");

        aggregate = new AggregateExpression(contextTable, aggregateFunction);
        return true;
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

        if (filter is ExistsConditionDefinition exists)
            return ParseExistsPredicate(tableContext, exists, depth + 1);

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

    private SqlExpression ParseExistsPredicate(
        TableExpression outerTable,
        ExistsConditionDefinition exists,
        int depth)
    {
        if (exists.Context.Value is not string contextName ||
            string.IsNullOrWhiteSpace(contextName))
            throw new FormatException("An exists condition requires a context name.");

        var source = outerTable.Entity.Relations?.Any(relation =>
            string.Equals(relation.Name, contextName, StringComparison.OrdinalIgnoreCase)) == true
            ? JoinCollection(outerTable, contextName)
            : outerTable.JoinedTables.TryGetValue(contextName, out var joined)
                ? joined
                : CreateTableContext(GetEntity(contextName));

        var subquery = source.CloneForSubquery();
        if (exists.Condition is not null)
            subquery.SqlWhere.Add(ParseExistsCondition(outerTable, subquery, exists, exists.Condition, depth));

        return new AnyExpression(subquery, exists.Negated);
    }

    private SqlExpression ParseExistsCondition(
        TableExpression outerTable,
        TableExpression innerTable,
        ExistsConditionDefinition exists,
        IConditionDefintion condition,
        int depth)
    {
        if (condition is ConditionDefinition group)
        {
            SqlExpression? result = null;
            foreach (var child in group.Filters)
            {
                var expression = ParseExistsCondition(outerTable, innerTable, exists, child, depth + 1);
                if (result is null)
                {
                    result = expression;
                    continue;
                }

                var logicalOperation = child is ConditionStatementDefinition { LogicalOperator: { } logical }
                    ? logical
                    : group.LogicalOperation;
                result = new BinarySqlExpression(
                    result,
                    logicalOperation.ToString().ToUpperInvariant(),
                    expression,
                    typeof(bool));
            }

            return result ?? throw new FormatException("An exists condition cannot be empty.");
        }

        if (condition is ExistsConditionDefinition nested)
            return ParseExistsPredicate(innerTable, nested, depth + 1);

        if (condition is not ConditionStatementDefinition statement)
            throw new FormatException($"Unsupported exists condition [{condition.GetType().Name}].");

        var left = ParseExistsExpression(outerTable, innerTable, exists, statement.Left, depth + 1);
        if (statement.Operator is BinaryOperator.IsNull or BinaryOperator.IsNotNull)
            return new PredicateExpression(
                left,
                statement.Operator == BinaryOperator.IsNull ? "IS NULL" : "IS NOT NULL");

        var right = statement.Right
            ?? throw new FormatException($"Predicate operator [{statement.Operator}] requires a right operand.");

        if (statement.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            if (right is not GroupFieldDefintion { Fields.Length: > 0 } values)
                throw new FormatException("An exists set comparison requires values.");
            return new SetPredicateExpression(
                left,
                statement.Operator == BinaryOperator.In ? "IN" : "NOT IN",
                values.Fields.Select(value =>
                    ParseExistsExpression(outerTable, innerTable, exists, value, depth + 1)).ToArray());
        }

        if (statement.Operator is BinaryOperator.Between or BinaryOperator.NotBetween)
        {
            if (right is not GroupFieldDefintion { Fields.Length: 2 } range)
                throw new FormatException("An exists range comparison requires two bounds.");
            return new RangePredicateExpression(
                left,
                statement.Operator == BinaryOperator.Between ? "BETWEEN" : "NOT BETWEEN",
                ParseExistsExpression(outerTable, innerTable, exists, range.Fields[0], depth + 1),
                ParseExistsExpression(outerTable, innerTable, exists, range.Fields[1], depth + 1));
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
            _ => throw new FormatException($"Unsupported exists predicate operator [{statement.Operator}].")
        };

        return new BinarySqlExpression(
            left,
            operation,
            ParseExistsExpression(outerTable, innerTable, exists, right, depth + 1),
            typeof(bool));
    }

    private SqlExpression ParseExistsExpression(
        TableExpression outerTable,
        TableExpression innerTable,
        ExistsConditionDefinition exists,
        IFieldDefinition expression,
        int depth)
    {
        if (expression is FieldDefinition field)
        {
            var separator = field.FieldName.IndexOf('.');
            if (separator > 0)
            {
                var prefix = field.FieldName[..separator];
                var remaining = field.FieldName[(separator + 1)..];
                if (string.Equals(prefix, "CurrentContext", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefix, outerTable.Entity.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefix, outerTable.Alias, StringComparison.OrdinalIgnoreCase))
                    return ParseFieldPath(outerTable, remaining);

                if (string.Equals(prefix, exists.Alias, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefix, exists.Context.Value?.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefix, innerTable.Entity.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefix, innerTable.Alias, StringComparison.OrdinalIgnoreCase))
                    return ParseFieldPath(innerTable, remaining);
            }

            return ParseFieldPath(innerTable, field.FieldName);
        }

        if (expression is GroupFieldDefintion group)
        {
            var fields = group.Fields
                .Select(field => ParseExistsExpression(outerTable, innerTable, exists, field, depth + 1))
                .ToArray();
            return ParseQueryExpression(innerTable, group with { Fields = group.Fields }, depth + 1);
        }

        return ParseQueryExpression(innerTable, expression, depth + 1);
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
        if (FieldDefinitionParser.Parse(fieldName) is not FunctionFieldDefinition parsedFunction ||
            !functionProvider.TryGetFunction(parsedFunction.FunctionName, out var definition) ||
            definition is not { IsAggregate: true })
            return false;

        function = parsedFunction;
        return true;
    }

    private bool TryParseFunction(string fieldName, out FunctionFieldDefinition function)
    {
        function = null!;
        if (FieldDefinitionParser.Parse(fieldName) is not FunctionFieldDefinition parsedFunction ||
            !functionProvider.TryGetFunction(parsedFunction.FunctionName, out _))
            return false;

        function = parsedFunction;
        return true;
    }

    public TableExpression JoinCollection(TableExpression tableContext, string? relationPath)
    {
        var joinedData = ParseRelationPath(tableContext, relationPath);
        var lastTable = ApplyJoin(tableContext, joinedData.Take(joinedData.Count() - 1));

        var (relation, entity) = joinedData.Last();
        var joinedTable = CreateTableContext(entity);
        joinedTable.SqlWhere.AddRange(BuildJoinKeys(lastTable, joinedTable, relation));

        return joinedTable;
    }

    private IEnumerable<(Relation relation, IEntity joinedEntity)> ParseRelationPath(TableExpression table, string? relationPath)
    {
        var next = table.Entity;

        return string.IsNullOrEmpty(relationPath)
            ? Enumerable.Empty<(Relation relation, IEntity joinedEntity)>()
            : relationPath.Split('.')
                .Select(e =>
                {
                    var rel = next.GetRelation(e);
                    next = metaProvider.GetEntity(rel.RefEntity);
                    return (rel, next);
                })
                .ToList();
    }

    private TableExpression ApplyJoin(TableExpression tableContext, IEnumerable<(Relation relation, IEntity joinedEntity)> joinedData)
    {
        var lastTable = tableContext;
        foreach (var (relation, joinedEntity) in joinedData)
        {
            if (!lastTable.JoinedTables.TryGetValue(relation.Name, out TableExpression? joinedTable))
            {
                joinedTable = CreateTableContext(joinedEntity);
                joinedTable.JoinRoot = tableContext.JoinHost;
                lastTable.JoinedTables[relation.Name] = joinedTable;
                tableContext.JoinHost.Joins.Add(BuildJoinPath(lastTable, joinedTable, relation));
            }
            lastTable = joinedTable;
        }

        return lastTable;
    }

    private TableExpression ApplyJoinAggregate(TableExpression tableContext, IEnumerable<(Relation relation, IEntity joinedEntity)> joinedData)
    {
        var lastTable = tableContext;
        if (joinedData.Any())
        {
            List<SqlExpression>? predicates = null;
            foreach (var (relation, joinedEntity) in joinedData)
            {
                var joinedTable = CreateTableContext(joinedEntity);
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

    private AggregateExpression ApplyGroupedJoinAggregate(
        TableExpression tableContext,
        Relation relation,
        IEntity joinedEntity,
        FunctionFieldDefinition aggregateDefinition)
    {
        var relationKey = relation.Name;
        if (!tableContext.GroupedAggregateJoins.TryGetValue(relationKey, out var grouped))
        {
            var source = CreateTableContext(joinedEntity);
            var keyFields = relation.RefKeys
                .Select(key => new Field(key.RefDbColumn, key.RefDbColumn))
                .ToArray();
            grouped = new GroupedAggregateTableExpression(source, GetNextAlias(), keyFields);
            tableContext.GroupedAggregateJoins[relationKey] = grouped;
            var conditions = relation.RefKeys
                .Select(key => (SqlExpression)new BinarySqlExpression(
                    new FieldExpression(grouped.Alias, new Field(key.RefDbColumn, key.RefDbColumn)),
                    "=",
                    new FieldExpression(tableContext.Alias, new Field(key.DbColumn, key.DbColumn)),
                    typeof(bool)))
                .ToArray();
            tableContext.JoinHost.Joins.Add(new JoinExpression(grouped, "LEFT JOIN", conditions));
        }

        var function = ParseFunctionExpression(grouped.Source, aggregateDefinition, 0) as FunctionSqlExpression
            ?? throw new InvalidOperationException(
                $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
        var aggregateAlias = grouped.AddAggregate(function);
        var value = new FieldExpression(grouped.Alias, new Field(aggregateAlias, aggregateAlias));
        var isCount = string.Equals(aggregateDefinition.FunctionName, "Count", StringComparison.OrdinalIgnoreCase);
        return new AggregateExpression(grouped, function, groupedJoinValue: value, groupedJoinCount: isCount);
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
