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

            case nameof(QueryValue.Count):
            case nameof(QueryValue.Sum):
            case nameof(QueryValue.Average):
                return CreateCollectionAggregate(node);

            default:
                throw new NotImplementedException(string.Format("not support method {0}.{1}", node.Method.DeclaringType?.Name, node.Method.Name));
        }
    }

    private SqlExpression CreateCollectionAggregate(MethodCallExpression node)
    {
        if (node.Object is not MethodCallExpression
            {
                Method.Name: "get_Item",
                Object: ParameterExpression parameter
            } fieldAccess ||
            fieldAccess.Arguments[0] is not ConstantExpression { Value: string fieldPath })
            throw new FormatException(
                $"{node.Method.Name}() requires a field or collection path.");

        var tableContext = QueryContext.Arguments
            .First(e => e.ArgumentName == parameter.Name)
            .TableContext;
        var lastSeparator = fieldPath.LastIndexOf('.');
        var relationPath = lastSeparator < 0 ? fieldPath : fieldPath[..lastSeparator];
        var fieldName = lastSeparator < 0 ? null : fieldPath[(lastSeparator + 1)..];
        if (fieldName is null)
        {
            try
            {
                var relation = QueryContext.GetEntity(tableContext.Entity.Name).GetRelation(relationPath);
                if (relation.Type != RelationType.Collection)
                    throw new FormatException(
                        $"{node.Method.Name}() requires a collection path; [{fieldPath}] is not a collection.");
                fieldName = QueryContext.GetFields(QueryContext.GetEntity(relation.RefEntity))
                    .First(field => string.Equals(field.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    .Name;
            }
            catch (RelationNotFoundException)
            {
                throw new FormatException(
                    $"{node.Method.Name}() requires a collection path; [{fieldPath}] is a scalar field.");
            }
        }

        return QueryContext.ParseFieldPath(
            tableContext,
            $"{relationPath}.{node.Method.Name.ToUpperInvariant()}({fieldName})");
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
