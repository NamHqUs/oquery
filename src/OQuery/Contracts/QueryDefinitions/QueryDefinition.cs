#pragma warning disable IDE0130
namespace OQuery;

using OQuery.Contracts.QueryDefinitions.Parsers;
#pragma warning restore IDE0130

public sealed class QueryDefinition
{
    public required string Entity { get; init; }
    public List<ProjectionDefinition> Projection { get; init; } = [];
    public ConditionDefinition? Filter { get; init; }
    public List<SortDefinition> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public List<QueryContextDefintion>? Contexts { get; set; }

    public static QueryDefinition FromYml(string yml)
        => QueryDefinitionParser.ParseFromYml(yml);

    public string ToYml()
    {
        List<string> lines = [];
        lines.Add($"Entity: {Entity}");

        if (Projection.Count > 0)
        {
            lines.Add("Projection:");
            lines.AddRange(Projection.Select(field => $"  - {field.ToText()}"));
        }

        if (Filter is not null)
        {
            lines.Add($"Filter: {Filter.ToText()}");
        }

        if (Sorts.Count > 0)
        {
            lines.Add($"Sorts: {string.Join(", ", Sorts.Select(FormatSort))}");
        }

        if (Page is not null)
            lines.Add($"Page: {Page}");

        if (PageSize is not null)
            lines.Add($"PageSize: {PageSize}");

        if (Contexts is { Count: > 0 })
        {
            lines.Add("Contexts:");
            foreach (var context in Contexts)
            {
                lines.Add($"  - Name: {context.Name}");
                if (!string.IsNullOrWhiteSpace(context.Alias))
                    lines.Add($"    Alias: {context.Alias}");
                lines.Add($"    JoinType: {context.JoinType}");
                if (context.Recursive)
                    lines.Add("    Recursive: true");
                lines.Add("    Query:");
                lines.AddRange(context.Query.ToYml()
                    .Split(Environment.NewLine)
                    .Select(line => $"      {line}"));
                if (context.Condition is not null)
                    lines.Add($"    Condition: {FormatContextCondition(context.Condition)}");
            }
        }

        return string.Join(Environment.NewLine, lines.Where(line => line is not null)!);
    }

    public override string ToString()
        => ToYml();

    private static string FormatSort(SortDefinition sort)
        => $"{sort.Field.ToText()} {sort.Direction switch
        {
            QuerySortDirection.Ascending => "ASC",
            QuerySortDirection.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(sort.Direction))
        }}";

    private static string FormatContextCondition(IConditionDefintion condition)
        => condition switch
        {
            ConditionDefinition group => FormatConditionGroup(group),
            ConditionStatementDefinition statement => FormatConditionStatement(statement),
            _ => throw new FormatException($"Unsupported context condition [{condition.GetType().Name}].")
        };

    private static string FormatConditionGroup(ConditionDefinition group)
    {
        var text = new System.Text.StringBuilder();
        for (var index = 0; index < group.Filters.Count; index++)
        {
            var child = group.Filters[index];
            if (index > 0)
            {
                var operation = child is ConditionStatementDefinition { LogicalOperator: { } logicalOperator }
                    ? logicalOperator
                    : group.LogicalOperation;
                text.Append(' ').Append(operation.ToString().ToUpperInvariant()).Append(' ');
            }

            if (child is ConditionDefinition)
                text.Append('(').Append(FormatContextCondition(child)).Append(')');
            else
                text.Append(FormatContextCondition(child));
        }

        return text.ToString();
    }

    private static string FormatConditionStatement(ConditionStatementDefinition statement)
    {
        var operation = statement.OperatorId ?? statement.Operator switch
        {
            BinaryOperator.Equals => "=",
            BinaryOperator.NotEquals => "<>",
            BinaryOperator.Like => "LIKE",
            BinaryOperator.NotLike => "NOT LIKE",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.LessThan => "<",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.Between => "BETWEEN",
            BinaryOperator.NotBetween => "NOT BETWEEN",
            BinaryOperator.In => "IN",
            BinaryOperator.NotIn => "NOT IN",
            BinaryOperator.IsNull => "IS NULL",
            BinaryOperator.IsNotNull => "IS NOT NULL",
            _ => throw new ArgumentOutOfRangeException(nameof(statement.Operator))
        };
        var right = statement.Right is GroupFieldDefintion group &&
            statement.Operator is BinaryOperator.Between or BinaryOperator.NotBetween or BinaryOperator.In or BinaryOperator.NotIn
                ? $"<{string.Join(", ", group.Fields.Select(FormatJoinExpression))}>"
                : statement.Right is null
                    ? string.Empty
                    : $" {FormatJoinExpression(statement.Right)}";

        return $"{statement.Left.ToText()} {operation}{right}";
    }

    private static string FormatJoinExpression(IFieldDefinition expression)
        => expression switch
        {
            ConstFieldDefinition { Value: string value } => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'",
            ConstFieldDefinition { Value: null } => "null",
            ConstFieldDefinition { Value: IFormattable value } =>
                value.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            GroupFieldDefintion group => FormatArithmeticExpression(group.Fields),
            _ => expression.ToText()
        };

    private static string FormatArithmeticExpression(IReadOnlyList<IFieldDefinition> fields)
        => string.Join(
            " ",
            fields.Select((field, index) =>
                $"{(index == 0 ? string.Empty : field.Oper.ToSymbol() + " ")}{FormatJoinExpression(field)}"));

}
