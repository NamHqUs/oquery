namespace OQuery.Compilation.Expressions;

internal sealed class ProjectionAliasExpression(string alias, Type type)
    : SqlExpression(type)
{
    public string Alias { get; } = alias;
}
