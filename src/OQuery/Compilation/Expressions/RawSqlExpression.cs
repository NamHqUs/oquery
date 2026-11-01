namespace OQuery.Compilation.Expressions;

internal sealed class RawSqlExpression(string sql, Type type) : SqlExpression(type)
{
    public string Sql { get; } = sql;
}
