using System.Linq.Expressions;

namespace OQuery.Compilation.Utilities;

static class ExpressionHelper
{
    public static Expression StripQuotes(this Expression expression)
    {
        while (expression.NodeType == ExpressionType.Quote)
            expression = ((UnaryExpression)expression).Operand;

        return expression;
    }
}
