using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.QueryDefinitions.Parsers;
using OQuery.QueryBuilderDto.Parsers;
using static OQuery.SqlFunctionDefintion;

namespace OQuery.Compilation;

class QueryCompilationResolver(
    IMetadataProvider metaProvider,
    IFunctionProvider functionProvider,
    ISqlDialect sqlDialect,
    CompilationState state)
{
    private readonly CompilationState _state = state;
    private readonly Dictionary<GroupedAggregateTableExpression, TableExpression> _outerCopies = new();
    public Stack<(string ArgumentName, TableExpression TableContext)> Arguments => _state.Arguments;

    public IEnumerable<DbParameter> DbParameters => _state.DbParameters;
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

    public IFunctionProvider FunctionProvider => functionProvider;

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
        if (TryResolveRootFormField(tableContext, fieldPath, out var outerField))
            return outerField;

        var projectionItem = tableContext.ProjectionItems
            .FirstOrDefault(item =>
                string.Equals(item.Alias, fieldPath, StringComparison.OrdinalIgnoreCase));
        var isMetadataField = tableContext.AuthorizedFields.Any(field =>
            string.Equals(field.Name, fieldPath, StringComparison.OrdinalIgnoreCase)) &&
            (tableContext.CommonTableExpressionName is null ||
             projectionItem?.Item is FieldExpression);
        if (!isMetadataField &&
            projectionItem
                is { Alias: var alias, Item: var projection })
            return new ProjectionAliasExpression(alias, projection.Type);
        if (!isMetadataField &&
            tableContext.PendingProjectionAliases.TryGetValue(fieldPath, out var pendingType))
            return new ProjectionAliasExpression(fieldPath, pendingType);

        if (TryParseScalarReceiverFunction(tableContext, fieldPath, out var receiverFunction))
            return ParseFunctionExpression(tableContext, receiverFunction, 0);

        if (TryParseFunction(fieldPath, out var wrappedFunction))
            return ParseFunctionExpression(tableContext, wrappedFunction, 0);

        var i = FindLastUnnestedDot(fieldPath);
        var relationPath = i < 0 ? null : fieldPath[..i];
        var fieldName = fieldPath[(i + 1)..];

        if (TryParseAggregateFunction(fieldName, out var aggregateFunction))
        {
            var aggregateJoinedData = ParseRelationPath(tableContext, relationPath).ToArray();
            if (TryParseRelationAggregate(tableContext, relationPath, aggregateFunction, 0, out var relationAggregate))
                return relationAggregate;

            var lastTable = ApplyJoinAggregate(tableContext, aggregateJoinedData);
            var function = (FunctionSqlExpression)ParseFunctionExpression(lastTable, aggregateFunction, 0);
            return new AggregateExpression(lastTable, function, !aggregateJoinedData.Any());
        }

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

        var joinedData = ParseRelationPath(tableContext, relationPath).ToArray();
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
        var previous = _state.ParseContextAggregatesAsScalar;
        _state.ParseContextAggregatesAsScalar = true;
        try
        {
            return ParseQueryExpression(tableContext, expression);
        }
        finally
        {
            _state.ParseContextAggregatesAsScalar = previous;
        }
    }

    public SqlExpression ExpandAggregatePipeline(
        SqlExpression expression,
        IList<ProjectionItem> pipelineItems)
    {
        if (expression is not FunctionSqlExpression function)
            return expression;

        var arguments = function.Arguments
            .Select(argument =>
            {
                if (argument is not FunctionSqlExpressionArgument expressionArgument)
                    return argument;

                var nested = ExpandAggregatePipeline(expressionArgument.Expression, pipelineItems);
                if (IsAggregateOver(function) && nested is FunctionSqlExpression nestedFunction &&
                    IsAggregateOver(nestedFunction))
                {
                    var alias = GetNextInternalAlias("pipeline");
                    pipelineItems.Add(new ProjectionItem(nestedFunction, alias, true));
                    nested = new ProjectionAliasExpression(alias, nested.Type);
                }

                return new FunctionSqlExpressionArgument(nested);
            })
            .ToArray();

        return new FunctionSqlExpression(
            function.Definition,
            arguments,
            function.IsDistinct,
            function.ContextTable,
            function.JoinConditions);
    }

    private static bool IsAggregateOver(FunctionSqlExpression function)
        => function.Definition.WindowOf is not null;

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
        if (TryRewriteWindowOverCollection(tableContext, expression, out var collectionCall))
            return ParseFunctionExpression(tableContext, collectionCall, depth + 1);

        if (TryResolveWindowStage(expression, out var windowCall))
            return ParseFunctionExpression(tableContext, windowCall, depth + 1);

        if (TryRewriteDottedCollectionFunction(expression, out var collectionForm))
            return ParseFunctionExpression(tableContext, collectionForm, depth + 1);

        if (TryParseCollectionFunction(tableContext, expression, depth, out var collectionFunction))
            return collectionFunction;

        if (TryRewriteRootCollectionAggregate(tableContext, expression, out var receiverPath, out var receiverCall) &&
            TryParseRelationAggregate(tableContext, receiverPath, receiverCall, depth, out var rootAggregate, rootForm: true))
            return rootAggregate;

        if (TryParseCollectionAggregate(tableContext, expression, depth, out var collectionAggregate))
            return collectionAggregate;

        if (expression.FunctionName.Contains('.'))
            return ParseFieldReceiverFunction(tableContext, expression, depth);

        var definition = functionProvider.GetFunction(expression.FunctionName);
        if (expression.IsDistinct && !definition.IsAggregate)
            throw new FormatException(
                $"Distinct is only supported for aggregate functions, not [{definition.Name}].");
        var suppliedArguments = expression.Arguments
            ?? throw new FormatException("A function's arguments cannot be null.");
        var minimumArgumentCount = definition.Arguments.Count(argument => !argument.Optional);
        if (suppliedArguments.Length < minimumArgumentCount ||
            suppliedArguments.Length > definition.Arguments.Count)
            throw new FormatException($"Unsupported function or argument count [{expression.FunctionName}].");

        // Trailing-bound functions fill the leading optional slots with their defaults first.
        var leadingDefaults = definition.BindArgumentsFromEnd
            ? definition.Arguments.Count - suppliedArguments.Length
            : 0;
        var arguments = new FunctionSqlArgument?[definition.Arguments.Count];
        for (var index = 0; index < definition.Arguments.Count; index++)
        {
            var argumentDefinition = definition.Arguments[index];
            var suppliedIndex = index - leadingDefaults;
            arguments[index] = suppliedIndex >= 0 && suppliedIndex < suppliedArguments.Length
                ? ParseFunctionArgument(tableContext, argumentDefinition, suppliedArguments[suppliedIndex], depth + 1)
                : ParseDefaultFunctionArgument(
                    tableContext,
                    argumentDefinition,
                    depth + 1,
                    renderAsLiteral: string.Equals(definition.Name, "Count", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(definition.Name, "Count", StringComparison.OrdinalIgnoreCase) &&
            arguments.Any(argument => argument is FunctionSqlTokenArgument { Token: "*" }))
        {
            throw new UnauthorizedAccessException(
                "COUNT(*) is not allowed. Specify an authorized field to count.");
        }

        if (_state.ParseContextAggregatesAsScalar &&
            TryParseContextAggregate(tableContext, expression, definition, depth, out var contextAggregate))
            return contextAggregate;

        return new FunctionSqlExpression(definition, arguments, expression.IsDistinct);
    }

    /// <summary>
    /// Rewrites the dotted form <c>Relation.Any(predicate)</c> to the function form <c>Any(Relation, predicate)</c>
    /// when the function is context-bound, so projections share the filter's collection rendering.
    /// </summary>
    private bool TryRewriteDottedCollectionFunction(FunctionFieldDefinition expression, out FunctionFieldDefinition rewritten)
    {
        rewritten = null!;
        var separator = expression.FunctionName.LastIndexOf('.');
        if (separator < 0)
            return false;

        var functionName = expression.FunctionName[(separator + 1)..];
        if (!functionProvider.TryGetFunction(functionName, out var definition) ||
            definition is not { RequiresContext: true })
            return false;

        var arguments = new IFieldDefinition[expression.Arguments.Length + 1];
        arguments[0] = new FieldDefinition(expression.Oper, expression.FunctionName[..separator]);
        expression.Arguments.CopyTo(arguments, 1);
        rewritten = expression with { FunctionName = functionName, Arguments = arguments };
        return true;
    }

    private bool TryParseCollectionFunction(
        TableExpression tableContext,
        FunctionFieldDefinition expression,
        int depth,
        out SqlExpression result)
    {
        result = null!;
        if (expression.FunctionName.Contains('.') ||
            !functionProvider.TryGetFunction(expression.FunctionName, out var definition) ||
            definition is not { RequiresContext: true })
            return false;
        if (expression.Arguments is not [FieldDefinition { FieldName: var relationPath }, .. var rest] ||
            rest.Length > 2)
            throw new FormatException($"{expression.FunctionName} requires a collection path and an optional predicate.");

        result = CreateCollectionFunction(
            expression.FunctionName,
            tableContext,
            relationPath,
            inner =>
            {
                if (rest.Length == 0)
                    return;
                var predicateArgument = rest.Length == 2 ? rest[1] : rest[0];
                var alias = rest.Length == 2 && rest[0] is FieldDefinition aliasField
                    ? aliasField.FieldName
                    : null;
                var predicateText = predicateArgument switch
                {
                    FieldDefinition field => field.FieldName,
                    ConditionFieldDefinition condition => condition.Condition.ToText(),
                    _ => throw new FormatException($"{expression.FunctionName} requires a predicate expression.")
                };
                if (alias is not null && predicateText.StartsWith(alias + ".", StringComparison.OrdinalIgnoreCase))
                    predicateText = predicateText[(alias.Length + 1)..];
                var parsedCondition = QueryDefinitions.Parsers.ConditionDefinitionParser.Parse(predicateText)
                    ?? throw new FormatException($"{expression.FunctionName} requires a predicate expression.");
                inner.SqlWhere.Add(ParsePredicateExpression(inner, parsedCondition, depth + 1));
            });
        return true;
    }

    /// <summary>
    /// Builds an existence test (Any, or NotAny when <paramref name="negated"/>) over a related collection,
    /// resolved from the function catalog by polarity.
    /// </summary>
    public SqlExpression CreateCollectionPredicate(
        bool negated,
        TableExpression tableContext,
        string relationPath,
        Action<TableExpression> addPredicates)
    {
        if (!functionProvider.TryGetCollectionPredicate(negated, out var definition))
            throw new FormatException("No collection predicate is registered for this dialect.");
        return CreateCollectionFunction(definition!.Name, tableContext, relationPath, addPredicates);
    }

    /// <summary>
    /// Builds a call to a context-bound function (one whose template uses @Context) over a related collection.
    /// The inner source and its correlation predicates are kept separate so the generator can render
    /// them into @Context and @JoinedConditions; <paramref name="addPredicates"/> adds the optional predicate to the inner source.
    /// </summary>
    public SqlExpression CreateCollectionFunction(
        string functionName,
        TableExpression tableContext,
        string relationPath,
        Action<TableExpression> addPredicates)
    {
        var definition = functionProvider.GetFunction(functionName);
        if (!definition.RequiresContext)
            throw new FormatException($"Function [{functionName}] cannot be applied to a collection.");

        var inner = JoinCollection(tableContext, relationPath);
        var joinConditions = inner.SqlWhere.ToArray();
        inner.SqlWhere.Clear();
        addPredicates(inner);
        var predicates = inner.SqlWhere.ToArray();
        inner.SqlWhere.Clear();

        if (predicates.Length == 0)
            return new FunctionSqlExpression(definition, CreateCollectionArguments(definition, null), false, inner, joinConditions);

        var predicate = predicates.Aggregate((left, right) =>
            new BinarySqlExpression(left, "AND", right, typeof(bool)));
        return new FunctionSqlExpression(
            definition,
            CreateCollectionArguments(definition, new FunctionSqlExpressionArgument(predicate)),
            false,
            inner,
            joinConditions);
    }

    private static FunctionSqlArgument?[] CreateCollectionArguments(
        SqlFunctionDefintion definition,
        FunctionSqlArgument? predicate)
    {
        if (definition.Arguments.Count == 0)
        {
            if (predicate is not null)
                throw new FormatException($"Function [{definition.Name}] does not accept a predicate.");
            return [];
        }

        return [predicate];
    }

    private SqlExpression ParseFieldReceiverFunction(TableExpression tableContext, FunctionFieldDefinition expression, int depth)
    {
        var separator = expression.FunctionName.LastIndexOf('.');
        var receiverPath = expression.FunctionName[..separator];
        var functionName = expression.FunctionName[(separator + 1)..];
        var definition = functionProvider.GetFunction(functionName);
        if (definition.IsAggregate)
            throw new FormatException($"Aggregate function [{functionName}] requires a collection receiver, not a scalar field [{receiverPath}].");
        if (definition.Arguments.Count == 0)
            throw new FormatException($"Function [{functionName}] cannot be called on a field receiver.");

        var receiver = ParseFieldPath(tableContext, receiverPath);
        if (receiver is not FieldExpression { MetaField: var receiverField })
            throw new FormatException($"Function receiver [{receiverPath}] must be a scalar field.");
        if (definition.Arguments[0].DataType != receiverField.DataType)
            throw new FormatException(
                $"Function [{functionName}] cannot be applied to [{receiverPath}] of type [{receiverField.DataType}]; its first argument is [{definition.Arguments[0].DataType}].");

        var suppliedArguments = expression.Arguments
            ?? throw new FormatException("A function's arguments cannot be null.");
        var arguments = new IFieldDefinition[suppliedArguments.Length + 1];
        arguments[0] = new FieldDefinition(expression.Oper, receiverPath);
        suppliedArguments.CopyTo(arguments, 1);
        return ParseFunctionExpression(
            tableContext,
            expression with { FunctionName = functionName, Arguments = arguments },
            depth + 1);
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


    // A window stage ("Source.Avg()") or a window call ("CountOver(Relation)") over a collection relation is the
    // collection aggregate over that relation. The aggregate is read from the window function's catalog link.
    private bool TryRewriteWindowOverCollection(
        TableExpression tableContext,
        FunctionFieldDefinition expression,
        out FunctionFieldDefinition rewritten)
    {
        rewritten = null!;
        if (!TryGetWindowAggregateName(expression, out var aggregateName) ||
            expression.Arguments is not [FieldDefinition { FieldName: var receiver }] ||
            receiver.Contains('.') ||
            tableContext.AuthorizedFields.Any(field => string.Equals(field.Name, receiver, StringComparison.OrdinalIgnoreCase)) ||
            tableContext.Entity.Relations?.Any(relation =>
                relation.Type == RelationType.Collection &&
                string.Equals(relation.Name, receiver, StringComparison.OrdinalIgnoreCase)) != true)
            return false;

        rewritten = new FunctionFieldDefinition($"{receiver}.{aggregateName}", []);
        return true;
    }

    private bool TryGetWindowAggregateName(FunctionFieldDefinition expression, out string aggregateName)
    {
        aggregateName = expression.FunctionName;
        if (expression.IsWindow)
            return true;
        if (functionProvider.TryGetFunction(expression.FunctionName, out var definition) &&
            definition.WindowOf is { } windowOf)
        {
            aggregateName = windowOf;
            return true;
        }

        return false;
    }

    private bool TryResolveWindowStage(FunctionFieldDefinition expression, out FunctionFieldDefinition windowCall)
    {
        windowCall = null!;
        if (!expression.IsWindow)
            return false;
        if (!functionProvider.TryGetWindowFunction(expression.FunctionName, out var window))
            throw new FormatException(
                $"Aggregate [{expression.FunctionName}] has no window form for a pipeline stage.");

        windowCall = expression with { FunctionName = window!.Name, IsWindow = false };
        return true;
    }

    private bool TryParseRelationAggregate(
        TableExpression tableContext,
        string? relationPath,
        FunctionFieldDefinition aggregateDefinition,
        int depth,
        out AggregateExpression aggregate,
        bool rootForm = false)
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

        if (tableContext.JoinedTables.TryGetValue(relationPath, out var joinedContext) &&
            tableContext.Entity.Relations?.Any(relation =>
                string.Equals(relation.Name, relationPath, StringComparison.OrdinalIgnoreCase)) != true)
        {
            aggregate = ApplyGroupedJoinedAggregate(
                tableContext,
                relationPath,
                joinedContext,
                aggregateDefinition);
            return true;
        }

        var joinedData = ParseRelationPath(tableContext, relationPath).ToArray();
        var firstCollection = Array.FindIndex(joinedData, step => step.relation.Type == RelationType.Collection);
        if (firstCollection >= 0)
        {
            var outer = ApplyJoin(tableContext, joinedData.Take(firstCollection));
            var (relation, joinedEntity) = joinedData[firstCollection];
            aggregate = ApplyGroupedJoinAggregate(
                outer,
                relation,
                joinedEntity,
                aggregateDefinition,
                joinedData.Skip(firstCollection + 1),
                rootForm);
            return true;
        }

        var lastTable = ApplyJoinAggregate(tableContext, joinedData);
        var function = ParseFunctionExpression(lastTable, aggregateDefinition, depth + 1) as FunctionSqlExpression
            ?? throw new InvalidOperationException(
                $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
        aggregate = new AggregateExpression(lastTable, function, !joinedData.Any());
        return true;
    }

    private AggregateExpression ApplyGroupedJoinedAggregate(
        TableExpression tableContext,
        string joinedAlias,
        TableExpression joinedTable,
        FunctionFieldDefinition aggregateDefinition)
    {
        var join = tableContext.JoinHost.Joins.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Table, joinedTable) ||
            string.Equals(candidate.Table.Alias, joinedTable.Alias, StringComparison.OrdinalIgnoreCase));
        if (join is null)
        {
            var fallbackDefinition = RemoveTableQualifier(
                aggregateDefinition,
                joinedAlias);
            var fallbackFunction = ParseFunctionExpression(joinedTable, fallbackDefinition, 0)
                as FunctionSqlExpression
                ?? throw new InvalidOperationException(
                    $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
            return new AggregateExpression(joinedTable, fallbackFunction, groupWithQuery: true);
        }

        if (join.Conditions.Count == 0)
        {
            var fallbackDefinition = RemoveTableQualifier(
                aggregateDefinition,
                joinedAlias);
            var fallbackFunction = ParseFunctionExpression(joinedTable, fallbackDefinition, 0)
                as FunctionSqlExpression
                ?? throw new InvalidOperationException(
                    $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
            return new AggregateExpression(joinedTable, fallbackFunction, groupWithQuery: true);
        }

        var keyPairs = join.Conditions
            .Select(condition => condition switch
            {
                BinarySqlExpression
                {
                    Operator: "=",
                    Left: FieldExpression left,
                    Right: FieldExpression right
                } when string.Equals(left.TableAlias, joinedTable.Alias, StringComparison.OrdinalIgnoreCase)
                    => (JoinedField: left.MetaField, ParentExpression: (SqlExpression)right),
                BinarySqlExpression
                {
                    Operator: "=",
                    Left: FieldExpression left,
                    Right: FieldExpression right
                } when string.Equals(right.TableAlias, joinedTable.Alias, StringComparison.OrdinalIgnoreCase)
                    => (JoinedField: right.MetaField, ParentExpression: (SqlExpression)left),
                _ => default
            })
            .Where(pair => pair.JoinedField is not null)
            .ToArray();
        if (keyPairs.Length == 0)
            throw new FormatException(
                $"Collection aggregate receiver [{joinedAlias}] requires an equality join key.");

        var relationKey = $"context:{joinedAlias}";
        if (!tableContext.GroupedAggregateJoins.TryGetValue(relationKey, out var grouped))
        {
            var keyFields = keyPairs
                .Select(pair => pair.JoinedField)
                .DistinctBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            grouped = new GroupedAggregateTableExpression(joinedTable, GetNextAlias(), keyFields);
            tableContext.GroupedAggregateJoins[relationKey] = grouped;
            var conditions = keyPairs
                .Select(pair => (SqlExpression)new BinarySqlExpression(
                    new FieldExpression(grouped.Alias, pair.JoinedField),
                    "=",
                    pair.ParentExpression,
                    typeof(bool)))
                .ToArray();
            tableContext.JoinHost.Joins.Add(new JoinExpression(grouped, "LEFT JOIN", conditions));
        }

        var aggregateFunction = RemoveTableQualifier(
            aggregateDefinition,
            joinedAlias);
        var function = ParseFunctionExpression(grouped.Source, aggregateFunction, 0) as FunctionSqlExpression
            ?? throw new InvalidOperationException(
                $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
        var aggregateAlias = grouped.AddAggregate(function);
        var value = new FieldExpression(grouped.Alias, new Field(aggregateAlias, aggregateAlias));
        var isCount = string.Equals(aggregateDefinition.FunctionName, "Count", StringComparison.OrdinalIgnoreCase);
        return new AggregateExpression(grouped, function, groupedJoinValue: value, groupedJoinCount: isCount);
    }

    private static FunctionFieldDefinition RemoveTableQualifier(
        FunctionFieldDefinition aggregateDefinition,
        string tableAlias)
        => aggregateDefinition with
        {
            Arguments = aggregateDefinition.Arguments
                .Select(argument => argument switch
                {
                    FieldDefinition field when field.FieldName.StartsWith(
                        tableAlias + ".",
                        StringComparison.OrdinalIgnoreCase)
                        => field with { FieldName = field.FieldName[(tableAlias.Length + 1)..] },
                    _ => argument
                })
                .ToArray()
        };

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

        if (filter is FunctionCondition function)
        {
            if (functionProvider.GetFunction(function.Function.FunctionName).ResultType != DataType.Boolean)
                throw new FormatException(
                    $"Function [{function.Function.FunctionName}] cannot be used as a condition because it does not return a boolean.");
            return ParseQueryExpression(tableContext, function.Function, depth + 1);
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
        int depth,
        bool renderAsLiteral)
    {
        if (definition.DefaultValue is not { } defaultValue)
            return null;

        if (defaultValue is "*")
            return new FunctionSqlTokenArgument("*");
        // Count's default row marker is emitted as a SQL literal so Count() renders as COUNT(1).
        if (renderAsLiteral && defaultValue is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return new FunctionSqlExpressionArgument(new ValueExpression(defaultValue, typeof(decimal)));
        return new FunctionSqlExpressionArgument(
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

    // A fluent path such as HireDate.YearsToNow() calls a function on a scalar field; relation paths are left to the relation branches.
    private bool TryParseScalarReceiverFunction(
        TableExpression tableContext,
        string fieldPath,
        out FunctionFieldDefinition function)
    {
        function = null!;
        if (FieldDefinitionParser.Parse(fieldPath) is not FunctionFieldDefinition parsedFunction ||
            !parsedFunction.FunctionName.Contains('.'))
            return false;

        var separator = parsedFunction.FunctionName.LastIndexOf('.');
        var receiverPath = parsedFunction.FunctionName[..separator];
        var functionName = parsedFunction.FunctionName[(separator + 1)..];
        if (!functionProvider.TryGetFunction(functionName, out _) ||
            !tableContext.AuthorizedFields.Any(field =>
                string.Equals(field.Name, receiverPath, StringComparison.OrdinalIgnoreCase)))
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


    // Relation paths that a grouped source must join to read its raw (non-aggregate) arguments.
    // A nested aggregate is evaluated in its own grouped table, so it does not contribute to this key.
    private string[] ArgumentRelationPaths(IEnumerable<IFieldDefinition> arguments)
        => arguments
            .SelectMany(RawPathTexts)
            .SelectMany(text => System.Text.RegularExpressions.Regex
                .Matches(StripConstants(text), @"(?<![\w.])((?:[A-Za-z_]\w*\.)+)[A-Za-z_]\w*")
                .Select(match => match.Groups[1].Value.TrimEnd('.')))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private bool IsAggregateCall(FunctionFieldDefinition function)
        => function.FunctionName.Contains('.') ||
            (functionProvider.TryGetFunction(function.FunctionName, out var definition) && definition.IsAggregate);

    private IEnumerable<string> RawPathTexts(IFieldDefinition argument)
        => argument switch
        {
            FieldDefinition field => [field.FieldName],
            ConditionFieldDefinition condition => [condition.Condition.ToText()],
            GroupFieldDefintion group => group.Fields.SelectMany(RawPathTexts),
            FunctionFieldDefinition function when !IsAggregateCall(function) =>
                (function.Arguments ?? []).SelectMany(RawPathTexts),
            _ => [],
        };

    private static string StripConstants(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, @"'[^']*'|<[^>]*>", "''");

    private static readonly HashSet<string> PredicateKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "is", "null", "like", "in", "between", "true", "false",
    };

    // Internal marker for a root-context name inside a rewritten aggregate. Users never write it.
    private const string RootFieldMarker = "__root.";


    // is evaluated as Members.Sum(Total * Salary) with bare names bound to the call's context (the root). A member's
    // field is written through the receiver form. Anything else keeps the flat fallback.
    private bool TryRewriteRootCollectionAggregate(
        TableExpression tableContext,
        FunctionFieldDefinition expression,
        out string receiver,
        out FunctionFieldDefinition rewritten)
    {
        receiver = string.Empty;
        rewritten = null!;
        if (_state.AggregateScopes.Count > 0 ||
            expression.FunctionName.Contains('.') ||
            expression.Arguments is null ||
            expression.Arguments.Length == 0 ||
            !functionProvider.TryGetFunction(expression.FunctionName, out var definition) ||
            !definition.IsAggregate)
            return false;

        var fieldArguments = expression.Arguments.SelectMany(FieldTexts).ToArray();
        var match = (tableContext.Entity.Relations ?? [])
            .Where(relation => relation.Type == RelationType.Collection)
            .Select(relation => relation.Name)
            .FirstOrDefault(name => fieldArguments.Any(text => System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"(?<![\w.])" + System.Text.RegularExpressions.Regex.Escape(name) + @"\.",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
        if (match is null)
            return false;

        var prefix = new System.Text.RegularExpressions.Regex(
            @"(?<![\w.])" + System.Text.RegularExpressions.Regex.Escape(match) + @"\.",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var arguments = new IFieldDefinition[expression.Arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            if (!TryStripReceiverPrefix(expression.Arguments[index], match, prefix, out arguments[index]))
                return false;
        }

        receiver = match;
        rewritten = expression with { Arguments = arguments };
        return true;
    }

    private static bool TryStripReceiverPrefix(
        IFieldDefinition argument,
        string receiver,
        System.Text.RegularExpressions.Regex prefix,
        out IFieldDefinition stripped)
    {
        stripped = argument;
        switch (argument)
        {
            case ConstFieldDefinition:
                return true;
            case GroupFieldDefintion group:
            {
                var fields = new IFieldDefinition[group.Fields.Length];
                for (var index = 0; index < fields.Length; index++)
                {
                    if (!TryStripReceiverPrefix(group.Fields[index], receiver, prefix, out fields[index]))
                        return false;
                }

                stripped = group with { Fields = fields };
                return true;
            }
            case FieldDefinition field:
            {
                // Bare names are context-root references: they are marked before the receiver prefix is removed,
                // so a stripped member field stays a receiver field and is not mistaken for a root field.
                if (!OnlyDottedPathsFrom(field.FieldName, receiver))
                    return false;

                var marked = System.Text.RegularExpressions.Regex.Replace(
                    field.FieldName,
                    @"'[^']*'|<[^>]*>|(?<![\w.])([A-Za-z_]\w*)(?![\w.(])",
                    token => token.Groups[1].Success && !PredicateKeywords.Contains(token.Value)
                        ? RootFieldMarker + token.Value
                        : token.Value);
                stripped = new FieldDefinition(field.Oper, prefix.Replace(marked, string.Empty));
                return true;
            }
            default:
                return false;
        }
    }

    private static IEnumerable<string> FieldTexts(IFieldDefinition argument)
        => argument switch
        {
            FieldDefinition field => [field.FieldName],
            GroupFieldDefintion group => group.Fields.SelectMany(FieldTexts),
            _ => [],
        };

    // Every dotted path in the text must start with the receiver, so no path is re-scoped by the rewrite.
    private static bool OnlyDottedPathsFrom(string text, string receiver)
        => System.Text.RegularExpressions.Regex
            .Matches(text, @"(?<![\w.])([A-Za-z_]\w*)\.")
            .All(match => string.Equals(match.Groups[1].Value, receiver, StringComparison.OrdinalIgnoreCase));

    // A bare name in a root-form aggregate belongs to the call's context, the root row. That row is joined into the
    // grouped source on the relation keys, which is one-to-one with each receiver row, so it cannot multiply rows.
    private bool TryResolveRootFormField(TableExpression tableContext, string fieldPath, out SqlExpression result)
    {
        result = null!;
        if (!fieldPath.StartsWith(RootFieldMarker, StringComparison.Ordinal))
            return false;

        if (_state.AggregateScopes.Count == 0)
            throw new FormatException($"Root field [{fieldPath}] is only valid inside an aggregate.");

        var outer = GetOuterCopy(_state.AggregateScopes.Peek());
        result = ParseFieldPath(outer, fieldPath[RootFieldMarker.Length..]);
        return true;
    }

    private TableExpression GetOuterCopy(FunctionAggregateScope scope)
    {
        if (_outerCopies.TryGetValue(scope.Grouped, out var copy))
            return copy;

        copy = CreateTableContext(scope.OuterContext.Entity);
        var conditions = scope.Relation.RefKeys
            .Select(key => (SqlExpression)new BinarySqlExpression(
                new FieldExpression(copy.Alias, new Field(key.DbColumn, key.DbColumn)),
                "=",
                new FieldExpression(scope.Grouped.Source.Alias, new Field(key.RefDbColumn, key.RefDbColumn)),
                typeof(bool)))
            .ToArray();
        scope.Grouped.Source.JoinHost.Joins.Add(new JoinExpression(copy, "LEFT JOIN", conditions));
        _outerCopies[scope.Grouped] = copy;
        return copy;
    }

    private AggregateExpression ApplyGroupedJoinAggregate(
        TableExpression tableContext,
        Relation relation,
        IEntity joinedEntity,
        FunctionFieldDefinition aggregateDefinition,
        IEnumerable<(Relation relation, IEntity joinedEntity)> innerPath,
        bool rootForm)
    {
        var innerSteps = innerPath.ToArray();
        // Aggregates over different row sets must not share a grouped table: a deeper receiver hop, a
        // joined argument path, or a root-context join would multiply or re-scope the outer rows.
        var argumentPaths = ArgumentRelationPaths(aggregateDefinition.Arguments);
        var relationKey = string.Join(".", new[] { relation.Name }.Concat(innerSteps.Select(step => step.relation.Name)))
            + (argumentPaths.Length == 0 ? string.Empty : "|" + string.Join(",", argumentPaths))
            + (rootForm ? "|root" : string.Empty);
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

        // Hops after the grouped relation are joined inside the grouped source, so the
        // aggregate reads the same flat row set as an equivalent single-hop argument path.
        var aggregateSource = ApplyJoin(grouped.Source, innerSteps);
        _state.AggregateScopes.Push(new FunctionAggregateScope(grouped, tableContext, relation));
        FunctionSqlExpression function;
        try
        {
            function = ParseFunctionExpression(aggregateSource, aggregateDefinition, 0) as FunctionSqlExpression
                ?? throw new InvalidOperationException(
                    $"Aggregate function [{aggregateDefinition.FunctionName}] did not produce a SQL function.");
        }
        finally
        {
            _state.AggregateScopes.Pop();
        }

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
            var boolName = string.Format("@p{0}", _state.DbParameters.Count);
            _state.DbParameters.Add(new DbParameter(boolName, sqlDialect.NormalizeParameterValue(b)));
            return boolName;
        }

        if (!forceParameter && value is string st && !st.Contains('\''))
            return $"'{st}'";

        var name = string.Format("@p{0}", _state.DbParameters.Count);
        _state.DbParameters.Add(new DbParameter(name, sqlDialect.NormalizeParameterValue(value)));

        return name;
    }
    public string GetNextAlias() => "t" + _state.AliasCount++;

    public string GetNextInternalAlias(string name)
        => $"__oq_{name}_{_state.AliasCount++}";
}
