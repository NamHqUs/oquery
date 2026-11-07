namespace OQuery.Compilation.Expressions;

internal sealed class PredicateExpression(SqlExpression left, string @operator, SqlExpression? right = null)
    : SqlExpression(typeof(bool))
{
    public SqlExpression Left { get; } = left;
    public string Operator { get; } = @operator;
    public SqlExpression? Right { get; } = right;
}
