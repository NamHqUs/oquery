namespace OQuery.Compilation.Expressions;

internal sealed class SubqueryExpression(TableExpression query) : SqlExpression(query.Type)
{
    public TableExpression Query { get; } = query;
}
