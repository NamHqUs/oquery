namespace OQuery.Compilation.Expressions;

class AnyExpression(TableExpression tableContext) : TableExpression(tableContext.Entity, tableContext.Alias)
{
    private readonly TableExpression _tableContext = tableContext;

    public override Type Type => typeof(bool);

    internal TableExpression Query => _tableContext;
}
