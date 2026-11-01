using OQuery.Compilation.Expressions;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class ParameterVisitor(QueryContext queryContext) : QueryVisitor(queryContext)
{
    public SqlExpression Eval(MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case "get_Item":
                return CreateField(node);

            default:
                throw new NotImplementedException(string.Format("not support method {0}.{1}", node.Method.DeclaringType?.Name, node.Method.Name));
        }
    }

    //e => e["Lookup.FieldName"]
    //e => e.Field("Lookup.FieldName")
    private SqlExpression CreateField(MethodCallExpression node)
    {
        var argName = (node.Object as ParameterExpression)!.Name;
        var tableContext = QueryContext.Arguments.First(e => e.ArgumentName == argName).TableContext;

        var expression = Visit(node.Arguments[0]);
        var fieldPath = (string) Expression.Lambda(expression).Compile().DynamicInvoke()!;

        return QueryContext.ParseFieldPath(tableContext, fieldPath);
    }
}
