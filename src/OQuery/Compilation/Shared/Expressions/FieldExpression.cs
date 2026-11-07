namespace OQuery.Compilation.Expressions;

class FieldExpression(string tableAlias, Field field) : SqlExpression(typeof(QueryValue))
{
    public Field MetaField { get; } = field;
    public string TableAlias { get; } = tableAlias;

    internal override string DebugView
        => $"{GetType().Name}: {TableAlias}.{MetaField.Name}";

}
