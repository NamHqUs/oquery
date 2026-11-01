namespace OQuery.Compilation.Expressions;

class SkipExpression : TableExpression
{
    public SkipExpression(TableExpression tableContext, int numberOfRecords)
        : base(tableContext) => SkipCount = numberOfRecords;

    public override Type Type => typeof(int);
}
