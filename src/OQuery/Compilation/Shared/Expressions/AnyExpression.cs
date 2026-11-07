namespace OQuery.Compilation.Expressions;

class AnyExpression(TableExpression tableContext, bool negated = false) : TableExpression(tableContext)
{
    private readonly TableExpression _tableContext = tableContext;

    public override Type Type => typeof(bool);

    internal TableExpression Query => _tableContext;
    internal bool Negated { get; } = negated;
}
