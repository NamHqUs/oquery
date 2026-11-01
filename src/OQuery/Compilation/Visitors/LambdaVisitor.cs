using OQuery;
using OQuery.Compilation.Expressions;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

abstract class LambdaVisitor(TableExpression tableContext, QueryContext queryContext)
    : QueryVisitor(queryContext)
{
    protected TableExpression TableContext = tableContext;

    public TableExpression Eval(LambdaExpression lambda)
    {
        var hasSpecialField = TableContext.ProjectionItems?.Any(e => e.Item is not FieldExpression);
        if (hasSpecialField ?? false)
            TableContext = new TableContextWrapperExpression(TableContext, QueryContext.GetNextAlias());

        foreach (var e in lambda.Parameters)
            QueryContext.Arguments.Push((e.Name!, TableContext));

        var body = ((LambdaExpression)this.Visit(lambda)).Body;

        for (var i = 0; i < lambda.Parameters.Count; i++)
            QueryContext.Arguments.Pop();

        OnProcessResult(body);

        return TableContext;
    }

    protected abstract void OnProcessResult(Expression body);
}
