namespace OQuery.Compilation.Expressions;

internal sealed class RangePredicateExpression(
    SqlExpression left,
    string @operator,
    SqlExpression lower,
    SqlExpression upper)
    : SqlExpression(typeof(bool))
{
    public SqlExpression Left { get; } = left;
    public string Operator { get; } = @operator;
    public SqlExpression Lower { get; } = lower;
    public SqlExpression Upper { get; } = upper;
}
