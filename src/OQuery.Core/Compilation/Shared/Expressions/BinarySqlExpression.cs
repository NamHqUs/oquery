namespace OQuery.Compilation.Expressions;

internal sealed class BinarySqlExpression(
    SqlExpression left,
    string @operator,
    SqlExpression right,
    Type type,
    bool stringConcat = false) : SqlExpression(type)
{
    public SqlExpression Left { get; } = left;
    public string Operator { get; } = @operator;
    public SqlExpression Right { get; } = right;
    public bool StringConcat { get; } = stringConcat;
}
