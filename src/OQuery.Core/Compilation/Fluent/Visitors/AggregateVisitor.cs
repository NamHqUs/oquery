using OQuery.Compilation.Expressions;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class AggregateVisitor(TableExpression tableContext, QueryCompilationResolver queryContext)
{
    public AggregateExpression Eval(ConstantExpression expression)
    {
        var fieldName = (string)expression.Value!;
        return (AggregateExpression) queryContext.ParseFieldPath(tableContext, fieldName);
    }
}
