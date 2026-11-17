namespace OQuery.Compilation.Expressions;

internal sealed class ProjectionExpression(SqlExpression expression, string alias, bool parenthesize = false)
    : SqlExpression(expression.Type)
{
    public string Alias { get; } = alias;
    public SqlExpression Expression { get; } = expression;
    public bool Parenthesize { get; } = parenthesize;
}
