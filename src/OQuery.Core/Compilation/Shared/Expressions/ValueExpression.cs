namespace OQuery.Compilation.Expressions;

class ValueExpression(object? value, Type type) : SqlExpression(type)
{
    public readonly object? Value = value;

    internal override string DebugView
        => $"{GetType().Name}: {Value ?? "NULL"}";

}
