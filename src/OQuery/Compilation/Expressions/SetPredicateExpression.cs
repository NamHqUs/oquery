namespace OQuery.Compilation.Expressions;

internal sealed class SetPredicateExpression(
    SqlExpression left,
    string @operator,
    IReadOnlyList<SqlExpression> values)
    : SqlExpression(typeof(bool))
{
    public SqlExpression Left { get; } = left;
    public string Operator { get; } = @operator;
    public IReadOnlyList<SqlExpression> Values { get; } = values;
}
