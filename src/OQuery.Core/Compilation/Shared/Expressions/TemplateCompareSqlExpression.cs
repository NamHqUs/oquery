namespace OQuery.Compilation.Expressions;

internal sealed class TemplateCompareSqlExpression(
    SqlExpression left,
    SqlExpression right,
    string template,
    Type type) : SqlExpression(type)
{
    public SqlExpression Left { get; } = left;
    public SqlExpression Right { get; } = right;
    public string Template { get; } = template;
}
