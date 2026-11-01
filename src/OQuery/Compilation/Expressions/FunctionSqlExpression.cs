namespace OQuery.Compilation.Expressions;

internal sealed class FunctionSqlExpression(
    SqlFunctionDefintion definition,
    IReadOnlyList<FunctionSqlArgument?> arguments) : SqlExpression(ToClrType(definition.ResultType))
{
    public SqlFunctionDefintion Definition { get; } = definition;
    public IReadOnlyList<FunctionSqlArgument?> Arguments { get; } = arguments;

    private static Type ToClrType(DataType dataType)
        => dataType switch
        {
            DataType.String => typeof(string),
            DataType.Number => typeof(decimal),
            DataType.Datetime => typeof(DateTime),
            DataType.Boolean => typeof(bool),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported function result data type.")
        };
}

internal abstract record FunctionSqlArgument;

internal sealed record FunctionSqlExpressionArgument(SqlExpression Expression) : FunctionSqlArgument;

internal sealed record FunctionSqlTokenArgument(string Token) : FunctionSqlArgument;
