using OQuery.Compilation.Expressions;
using OQuery.Compilation.Utilities;
using OQuery.QueryBuilderDto.Parsers;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class ParameterVisitor(QueryCompilationResolver queryContext) : QueryVisitor(queryContext)
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

            case nameof(QueryValue.Any):
            case nameof(QueryValue.NotAny):
                return CreateCollectionAny(node, node.Method.Name == nameof(QueryValue.NotAny));

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
        var relationPath = fieldPath;
        Relation relation;
        try
        {
            relation = QueryContext.GetEntity(tableContext.Entity.Name).GetRelation(relationPath);
        }
        catch (RelationNotFoundException)
        {
            throw new FormatException(
                $"{node.Method.Name}() requires a collection relation; [{fieldPath}] is not a collection.");
        }
        if (relation.Type != RelationType.Collection)
            throw new FormatException(
                $"{node.Method.Name}() requires a collection relation; [{fieldPath}] is not a collection.");

        var fieldName = node.Method.Name.Equals(nameof(QueryValue.Count), StringComparison.OrdinalIgnoreCase)
            ? QueryContext.GetFields(QueryContext.GetEntity(relation.RefEntity))
                .First(field => string.Equals(field.Name, "Id", StringComparison.OrdinalIgnoreCase))
                .Name
            : null;
        if (node.Arguments.Count > 0)
        {
            if (node.Arguments[0] is not ConstantExpression { Value: string aggregateText } ||
                string.IsNullOrWhiteSpace(aggregateText))
                throw new FormatException(
                    $"{node.Method.Name}() requires a text field or expression argument.");
            fieldName = ParseAggregateExpression(aggregateText);
        }

        if (fieldName is null)
            throw new FormatException(
                $"{node.Method.Name}() requires an aggregate argument.");

        return QueryContext.ParseFieldPath(
            tableContext,
            $"{relationPath}.{node.Method.Name.ToUpperInvariant()}({fieldName})");
    }

    private static string ParseAggregateExpression(string text)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(?<![A-Za-z0-9_<])-?\d+(?:\.\d+)?(?![A-Za-z0-9_>])",
            match => $"<{match.Value}>");
        var expression = FieldDefinitionParser.Parse(normalized);
        return expression.ToText();
    }

    private SqlExpression CreateCollectionAny(MethodCallExpression node, bool negated)
    {
        if (node.Object is not MethodCallExpression
            {
                Method.Name: "get_Item",
                Object: ParameterExpression parameter
            } fieldAccess ||
            fieldAccess.Arguments[0] is not ConstantExpression { Value: string relationPath })
            throw new FormatException(
                $"{node.Method.Name}() requires a collection path.");

        var tableContext = QueryContext.Arguments
            .First(e => e.ArgumentName == parameter.Name)
            .TableContext;
        var predicate = (LambdaExpression)node.Arguments[0].StripQuotes();
        return QueryContext.CreateCollectionFunction(
            negated ? nameof(QueryValue.NotAny) : nameof(QueryValue.Any),
            tableContext,
            relationPath,
            innerTable => new WhereVisitor(innerTable, QueryContext).Eval(predicate));
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
