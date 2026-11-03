#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IConditionDefintion
{
    string ToText();
}

public class ConditionDefinition(LogicalOperator logicalOperator, params IConditionDefintion[] filter) : IConditionDefintion
{
    public ConditionDefinition(IEnumerable<IConditionDefintion> filter)
        : this(LogicalOperator.And, filter.ToArray())
    {
    }
    public LogicalOperator LogicalOperation { get; set; } = logicalOperator;
    public string Note { get; set; } = string.Empty;
    public List<IConditionDefintion> Filters { get; set; } = filter.ToList();

    public string ToText()
    {
        var text = new System.Text.StringBuilder();
        for (var index = 0; index < Filters.Count; index++)
        {
            var filter = Filters[index];
            if (index > 0)
            {
                var logicalOperation = filter is ConditionStatementDefinition { LogicalOperator: not null } item
                    ? item.LogicalOperator.Value
                    : LogicalOperation;
                text.Append(' ')
                    .Append(logicalOperation.ToString().ToUpperInvariant())
                    .Append(' ');
            }

            if (filter is ConditionDefinition)
                text.Append('(').Append(filter.ToText()).Append(')');
            else
                text.Append(filter.ToText());
        }

        return text.ToString();
    }
}

public record ConditionStatementDefinition(
    LogicalOperator? LogicalOperator,
    IFieldDefinition Left,
    BinaryOperator Operator,
    IFieldDefinition? Right = null) : IConditionDefintion
{
    public ConditionStatementDefinition(IFieldDefinition left, BinaryOperator @operator, IFieldDefinition? right = null)
        : this(null, left, @operator, right)
    {
    }

    public bool Parameterized { get; init; }
    public string? OperatorId { get; init; }
    public string? CustomSqlTemplate { get; init; }

    public string ToText()
        => $"{Left.ToText()} {OperatorId ?? FormatOperator(Operator)} {Right?.ToText() ?? "<NULL>"}";

    private static string FormatOperator(BinaryOperator operation)
        => operation switch
        {
            BinaryOperator.Equals => "=",
            BinaryOperator.NotEquals => "<>",
            BinaryOperator.Like => "LIKE",
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
            BinaryOperator.NotLike => "NOT LIKE",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
}

public enum BinaryOperator
{
    Equals,
    NotEquals,
    Like,
    NotLike,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Between,
    NotBetween,
    In,
    NotIn,
    IsNull,
    IsNotNull,
}

public enum LogicalOperator
{
    And,
    Or
}
