using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using System.Text;

namespace OQuery.Compilation;

internal sealed record SqlStatement(string CommandText, IReadOnlyList<DbParameter> Parameters);

internal sealed class SqlStatementGenerator(ISqlDialect dialect)
{
    private IReadOnlyDictionary<string, IEntity> _entities = new Dictionary<string, IEntity>();

    public SqlStatement Generate(TableExpression query, IEnumerable<DbParameter> parameters)
    {
        var text = RenderQuery(query);
        dialect.ValidateSqlStatement(text);

        return new SqlStatement(text, parameters.ToArray());
    }

    public SqlStatement GenerateTotalRecords(
        TableExpression query,
        IEnumerable<DbParameter> parameters,
        int maximumRecords)
    {
        query.EnsureDefaultProjection();
        var limitedQuery = new TakeExpression(query.WithoutPaging(), maximumRecords);
        var alias = dialect.QuoteIdentifier("__bounded_count");
        var text = $"{RenderCommonTableExpressions(query)}SELECT COUNT(1)\nFROM ({RenderQuery(limitedQuery, includeCtes: false)}) AS {alias}";
        dialect.ValidateSqlStatement(text);

        return new SqlStatement(text, parameters.ToArray());
    }

    private string RenderQuery(
        TableExpression query,
        bool exists = false,
        bool includeCtes = true,
        bool recursivePart = false)
    {
        var previousEntities = _entities;
        _entities = GetEntities(query, previousEntities);
        try
        {
            return RenderQueryCore(query, exists, includeCtes, recursivePart);
        }
        finally
        {
            _entities = previousEntities;
        }
    }

    private string RenderQueryCore(
        TableExpression query,
        bool exists = false,
        bool includeCtes = true,
        bool recursivePart = false)
    {
        query.EnsureDefaultProjection();
        var hasGroupedAggregate = query.ProjectionItems
            .Any(item => item.Item.ContainsGroupedAggregate());

        var select = exists
            ? "1"
            : query.ProjectionItems.Any()
                ? null
                : query.SqlSelect?.ToString();

        if (!exists && query is AggregateExpression aggregate && select is not null)
            select = RenderAggregateSelect(aggregate);

        if (select is null)
        {
            var projectionItems = query.ProjectionItems.ToArray();
            select = string.Join(
                string.Empty,
                projectionItems.Select((item, index) =>
                {
                    var separator = index == 0
                        ? string.Empty
                        : projectionItems[index - 1].Item is FieldExpression
                            && item.Item is FieldExpression
                            ? ", "
                            : ",\n";
                    return $"{separator}{RenderProjection(item, hasGroupedAggregate, recursivePart)}";
                }));
        }

        var takePrefix = query.IsTake ? dialect.RenderTakePrefix(query.Take!.Value) : string.Empty;
        if (takePrefix.Length != 0)
            select = takePrefix + select;

        var recursiveJoins = query.Joins
            .Select(join => new RecursiveJoin(
                join.JoinType.ToString(),
                RenderTable(join.Table),
                join.Conditions.Select(RenderExpression).ToArray()))
            .ToArray();
        var recursiveJoinRendering = recursivePart
            ? dialect.RenderRecursiveJoins(RenderTable(query), recursiveJoins)
            : new RecursiveJoinRendering(
                RenderTable(query) + string.Concat(recursiveJoins.Select(join =>
                    $"\n\t{join.JoinType} {join.Table} ON {string.Join(" AND ", join.Conditions)}")),
                []);
        IReadOnlyList<string> recursiveJoinConditions = recursivePart
            ? recursiveJoinRendering.AdditionalWhereConditions
            : [];
        var text = $"SELECT {select}\nFROM {recursiveJoinRendering.FromClause}";

        var whereExpressions = recursiveJoinConditions
            .Concat(query.SqlWhere.Select(RenderExpression))
            .ToArray();
        if (whereExpressions.Length != 0)
        {
            text += "\nWHERE " + whereExpressions[0];

            foreach (var where in whereExpressions.Skip(1))
                text += "\n\tAND " + where;
        }

        if (hasGroupedAggregate)
        {
            var groupBy = query.GroupByExpressions
                .Select(expression => (Sql: RenderExpression(expression), Fields: Array.Empty<string>(), Alias: (string?)null))
                .Concat(query.ProjectionItems
                    .Where(item => !item.Item.ContainsGroupedAggregate())
                    .Select(item => (
                        Sql: RenderExpression(item.Item),
                        Fields: FindFieldExpressions(item.Item)
                            .Select(RenderExpression)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        Alias: (string?)item.Alias)))
                .Select(group => dialect.RenderGroupField(group.Sql, group.Fields, group.Alias))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (groupBy.Length > 0)
                text += "\nGROUP BY " + string.Join(", ", groupBy);
        }

        if (query.SqlHaving.Count != 0)
        {
            text += "\nHAVING " + RenderExpression(query.SqlHaving[0]);

            foreach (var having in query.SqlHaving.Skip(1))
                text += "\n\tAND " + RenderExpression(having);
        }

        if (query.Orders.Count != 0)
            text += "\nORDER BY " + string.Join(", ", query.Orders.Select(order => RenderOrder(order, query)));

        text += dialect.RenderPaging(
            query.Skip,
            takePrefix.Length != 0 ? null : query.Take,
            query.Orders.Count != 0);

        if (includeCtes)
            text = RenderCommonTableExpressions(query) + text;

        return text;
    }

    private string RenderProjection(
        ProjectionItem item,
        bool hasGroupedAggregate = false,
        bool recursivePart = false)
    {
        if (hasGroupedAggregate && item.Item is AggregateExpression { GroupWithQuery: true } aggregate)
            return $"{RenderAggregateSelect(aggregate)} {dialect.QuoteIdentifier(item.Alias)}";

        var expression = RenderExpression(item.Item);
        if (recursivePart)
            expression = dialect.RenderRecursiveProjection(new(
                item.Item.Type,
                item.Item is FieldExpression,
                expression));
        if (item.Item is not FieldExpression and not FunctionSqlExpression)
            expression = $"({expression})";

        return $"{expression} {dialect.QuoteIdentifier(item.Alias)}";
    }

    private string RenderTable(TableExpression table)
        => $"{(table.CommonTableExpressionName is { } cteName
            ? dialect.RenderCteIdentifier(cteName)
            : dialect.RenderTable((Entity)table.Entity))} {table.Alias}";

    private string RenderCommonTableExpressions(TableExpression query)
    {
        if (query.CommonTableExpressions.Count == 0)
            return string.Empty;
        if (query.CommonTableExpressions.Any(cte => cte is RecursiveCommonTableExpression) &&
            !dialect.SupportsRecursiveCte)
            throw new NotSupportedException("The selected SQL dialect does not support recursive CTEs.");

        return $"WITH{(query.CommonTableExpressions.Any(cte => cte is RecursiveCommonTableExpression)
                && dialect.RequiresRecursiveCteKeyword
                ? " RECURSIVE"
                : string.Empty)} \n" +
            string.Join(",\n", query.CommonTableExpressions.Select(RenderCommonTableExpression)) + "\n";
    }

    private string RenderCommonTableExpression(CommonTableExpression cte)
        => cte switch
        {
            StandardCommonTableExpression standard =>
                $"{dialect.RenderCteIdentifier(standard.Name)} AS (\n{Indent(RenderQuery(standard.Query))}\n)",
            RecursiveCommonTableExpression recursive =>
                $"{dialect.RenderCteIdentifier(recursive.Name)} " +
                $"({string.Join(", ", recursive.Columns.Select(dialect.QuoteIdentifier))}) AS (\n" +
                $"{Indent(RenderQuery(recursive.Anchor, recursivePart: true))}\nUNION ALL\n" +
                $"{Indent(RenderQuery(recursive.Recursive, recursivePart: true))}\n)",
            _ => throw new ArgumentOutOfRangeException(nameof(cte))
        };

    private static string Indent(string text)
        => string.Join("\n", text.Split('\n').Select(line => "\t" + line));

    private string RenderOrder(OrderExpression order, TableExpression query)
    {
        var expression = order.ProjectionAlias is not null
            ? dialect.RenderOrderField(
                RenderExpression(order.Expression),
                FindFirstFieldExpression(order.Expression) is { } field
                    ? RenderExpression(field)
                    : null,
                order.ProjectionAlias,
                query.ProjectionItems
                    .Select((item, index) => (item, index))
                    .First(pair => string.Equals(pair.item.Alias, order.ProjectionAlias, StringComparison.OrdinalIgnoreCase))
                    .index + 1)
            : order.Expression is AggregateExpression { GroupWithQuery: true } aggregate
                ? RenderAggregateSelect(aggregate)
                : RenderExpression(order.Expression);

        return dialect.RenderOrder(expression, order.Descending);
    }

    private static FieldExpression? FindFirstFieldExpression(SqlExpression expression)
        => expression switch
        {
            FieldExpression field => field,
            FunctionSqlExpression function => function.Arguments
                .OfType<FunctionSqlExpressionArgument>()
                .Select(argument => FindFirstFieldExpression(argument.Expression))
                .FirstOrDefault(field => field is not null),
            BinarySqlExpression binary => FindFirstFieldExpression(binary.Left)
                ?? FindFirstFieldExpression(binary.Right),
            ProjectionExpression projection => FindFirstFieldExpression(projection.Expression),
            _ => null
        };

    private static IEnumerable<FieldExpression> FindFieldExpressions(SqlExpression expression)
        => expression switch
        {
            FieldExpression field => [field],
            FunctionSqlExpression function => function.Arguments
                .OfType<FunctionSqlExpressionArgument>()
                .SelectMany(argument => FindFieldExpressions(argument.Expression)),
            BinarySqlExpression binary => FindFieldExpressions(binary.Left)
                .Concat(FindFieldExpressions(binary.Right)),
            ProjectionExpression projection => FindFieldExpressions(projection.Expression),
            _ => []
        };

    private string RenderExpression(SqlExpression expression)
        => expression switch
        {
            FieldExpression field => RenderField(field),

            RawSqlExpression raw => raw.Sql,

            ValueExpression value => RenderValue(value.Value),

            BinarySqlExpression binary => RenderBinaryExpression(binary),

            TemplateCompareSqlExpression compare => RenderTemplateComparison(compare),

            RangePredicateExpression range =>
                $"({RenderExpression(range.Left)} {range.Operator} {RenderExpression(range.Lower)} AND {RenderExpression(range.Upper)})",

            SetPredicateExpression set =>
                $"({RenderExpression(set.Left)} {set.Operator} ({string.Join(", ", set.Values.Select(RenderExpression))}))",

            PredicateExpression predicate => predicate.Right is null
                ? predicate.Operator is "IS NULL" or "IS NOT NULL"
                    ? $"({RenderExpression(predicate.Left)} {predicate.Operator})"
                    : RenderExpression(predicate.Left)
                : $"({RenderExpression(predicate.Left)} {predicate.Operator} {RenderExpression(predicate.Right)})",

            ProjectionExpression projection => $"{(projection.Parenthesize ? "(" : "")}{RenderExpression(projection.Expression)}{(projection.Parenthesize ? ")" : "")} {dialect.QuoteIdentifier(projection.Alias)}",

            SubqueryExpression subquery => $"({RenderQuery(subquery.Query)})",

            AnyExpression any => $"EXISTS({RenderQuery(any.Query, true)})",

            AggregateExpression aggregate => aggregate.GroupWithQuery
                ? RenderAggregateSelect(aggregate)
                : $"({RenderAggregate(aggregate)})",

            FunctionSqlExpression function =>
                RenderFunction(function),

            _ => throw new NotSupportedException($"Unsupported SQL expression {expression.GetType().Name}")
        };

    private string RenderBinaryExpression(BinarySqlExpression binary)
    {
        var left = RenderExpression(binary.Left);
        var right = RenderExpression(binary.Right);
        var value = binary.StringConcat
            ? dialect.RenderStringConcatenation(left, right)
            : dialect.RenderArithmetic(left, binary.Operator, right);
        return $"({value})";
    }

    private string RenderField(FieldExpression field)
    {
        if (!_entities.TryGetValue(field.TableAlias, out var entity))
            throw new InvalidOperationException(
                $"The query field alias [{field.TableAlias}] could not be resolved during SQL generation.");

        if (field.MetaField.IsRawSql)
        {
            var rawSql = field.MetaField.DbColumn ?? field.MetaField.Name;
            return field.TableAlias.Length == 0
                ? $"({rawSql})"
                : $"({rawSql.Replace("@", field.TableAlias + ".")})";
        }

        var dbColumn = field.MetaField.DbColumn ?? field.MetaField.Name;
        return field.TableAlias.Length == 0
            ? $"({dbColumn})"
            : $"{field.TableAlias}.{dialect.QuoteIdentifier(dbColumn)}";
    }

    private static Dictionary<string, IEntity> GetEntities(
        TableExpression table,
        IReadOnlyDictionary<string, IEntity> inheritedEntities)
    {
        var entities = new Dictionary<string, IEntity>(
            inheritedEntities,
            StringComparer.OrdinalIgnoreCase)
        {
            [table.Alias] = table.Entity
        };
        AddJoinedEntities(table, entities, new HashSet<TableExpression>());
        return entities;
    }

    private static void AddJoinedEntities(
        TableExpression table,
        IDictionary<string, IEntity> entities,
        ISet<TableExpression> visited)
    {
        if (!visited.Add(table))
            return;

        foreach (var joinedTable in table.JoinedTables.Values)
        {
            entities[joinedTable.Alias] = joinedTable.Entity;
            AddJoinedEntities(joinedTable, entities, visited);
        }

        foreach (var join in table.Joins)
        {
            entities[join.Table.Alias] = join.Table.Entity;
            AddJoinedEntities(join.Table, entities, visited);
        }
    }

    private string RenderTemplateComparison(TemplateCompareSqlExpression compare)
        => "(" + compare.Template
            .Replace("{left}", RenderExpression(compare.Left), StringComparison.Ordinal)
            .Replace("{value}", RenderExpression(compare.Right), StringComparison.Ordinal) + ")";

    private string RenderFunction(FunctionSqlExpression function)
    {
        var arguments = function.Arguments
            .Select(argument => argument switch
            {
                FunctionSqlExpressionArgument expression => RenderExpression(expression.Expression),
                FunctionSqlTokenArgument token => token.Token,
                null => null,
                _ => throw new NotSupportedException(
                    $"Unsupported function argument type [{argument.GetType().Name}].")
            })
            .ToArray();
        var template = function.Definition.GetSqlTemplate(dialect.Key);
        var sql = new StringBuilder(template.Length);

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                sql.Append(template[index]);
                continue;
            }

            var close = template.IndexOf('}', index + 1);
            var placeholder = template.Substring(index + 1, close - index - 1);
            var argumentIndex = int.TryParse(placeholder, out var numericIndex)
                ? numericIndex
                : function.Definition.Arguments
                    .Select((argument, argumentIndex) => (argument, argumentIndex))
                    .First(pair => string.Equals(placeholder, pair.argument.Name, StringComparison.OrdinalIgnoreCase))
                    .argumentIndex + 1;
            sql.Append(arguments[argumentIndex - 1]
                ?? throw new FormatException(
                    $"SQL template for function [{function.Definition.Name}] references an omitted argument."));
            index = close;
        }

        return sql.ToString();
    }

    private string RenderAggregate(AggregateExpression aggregate)
    {
        aggregate.EnsureDefaultProjection();

        return RenderQuery(aggregate);
    }

    private string RenderAggregateSelect(AggregateExpression aggregate)
    {
        if (aggregate.FunctionExpression is not null)
            return RenderExpression(aggregate.FunctionExpression);

        var sql = aggregate.SelectSql;
        var result = new StringBuilder(sql.Length);

        for (var index = 0; index < sql.Length; index++)
        {
            if (sql[index] != '@')
            {
                result.Append(sql[index]);
                continue;
            }

            var fieldStart = index + 1;
            var fieldEnd = fieldStart;
            while (fieldEnd < sql.Length &&
                (char.IsLetterOrDigit(sql[fieldEnd]) || sql[fieldEnd] == '_'))
                fieldEnd++;

            if (fieldEnd == fieldStart)
            {
                result.Append('@');
                continue;
            }

            result.Append(aggregate.Alias)
                .Append('.')
                .Append(dialect.QuoteIdentifier(sql[fieldStart..fieldEnd]));
            index = fieldEnd - 1;
        }

        return result.ToString();
    }

    private string RenderValue(object? value)
        => value is null ? "NULL" :
            value is string s && !s.StartsWith("@", StringComparison.Ordinal) &&
                !(s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') &&
                !decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out _)
                ? dialect.RenderStringLiteral(s) : value.ToString()!;
}
