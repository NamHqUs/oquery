using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.Compilation.Utilities;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class SelectVisitor(TableExpression tableContext, QueryContext queryContext)
    : LambdaVisitor(tableContext, queryContext)
{
    protected override Expression VisitConstant(ConstantExpression node)
    {
        var value = node.Value;
        if (value is null || value is string || value is bool || value is IConvertible)
            return new ValueExpression(value, node.Type);
        return base.VisitConstant(node);
    }

    ///Dictionary Type .Select("Id", "Name") => .Select(Dictionary<string, object?>)
    protected override void OnProcessResult(Expression body)
    {
        if (body is ConstantExpression @const && @const.Value is Dictionary<string, object?> dataRecord)
        {
            TableContext.ProjectionItems = BuildProjection(TableContext, dataRecord).ToArray();
        }
    }

    //Dictionary Type .Select(e => new Dictionary<string, object?>{
    //        {"Field1, e["Field1"]}
    //        {"Field", e.Query("Collection").Count}, ...
    protected override Expression VisitListInit(ListInitExpression node)
    {
        if (node.Type != typeof(Dictionary<string, object?>))
            return base.VisitListInit(node);

        var dataRecord = new Dictionary<string, object?>();
        foreach (var e in node.Initializers)
        {
            var key = (string)((ConstantExpression)e.Arguments[0]).Value!;

            var expression = e.Arguments[1].StripQuotes();
            expression = expression is UnaryExpression unary ? unary.Operand : expression;
            object val = expression is ConstantExpression constant
                ? constant.Value!
                : Visit(expression);

            dataRecord[key] = (val as ConstantExpression)?.Value
                ?? ((val as UnaryExpression)?.Operand)
                ?? val;
        }

        return Expression.Constant(dataRecord);
    }

    //Calculate, ex:  e["FirstName"] + e["LastName"]
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        var leftVal = (SqlExpression)left;
        var rightVal = (SqlExpression)right;
        var operation = node.NodeType == ExpressionType.Add &&
            (ContainsStringConstant(node.Left) || ContainsStringConstant(node.Right))
                ? "+" : node.NodeType.ToSql();
        return new BinarySqlExpression(leftVal, operation, rightVal, node.Type,
            node.NodeType == ExpressionType.Add &&
            (ContainsStringConstant(node.Left) || ContainsStringConstant(node.Right)));
    }

    private static bool ContainsStringConstant(Expression node)
        => node is ConstantExpression { Value: string } ||
            node is BinaryExpression binary &&
            (ContainsStringConstant(binary.Left) || ContainsStringConstant(binary.Right));

    private IEnumerable<ProjectionItem> BuildProjection(
        TableExpression tableContext, IDictionary<string, object?> memebers, string? parentKey = null)
    {
        if (parentKey is null)
        {
            tableContext.ProjectionItems = memebers
                .Where(e => e.Value is not IDictionary<string, object?>)
                .Where(e => !e.Key.Contains('.', StringComparison.Ordinal))
                .Where(e => !QueryContext.GetFields(tableContext)
                    .Any(field => string.Equals(field.Name, e.Key, StringComparison.OrdinalIgnoreCase)))
                .Select(e => new ProjectionItem(
                    new ValueExpression(null, typeof(QueryValue)),
                    e.Key))
                .ToArray();
        }

        foreach (var e in memebers)
            if (e.Value is IDictionary<string, object?> subMembers)
                foreach (var projection in BuildProjection(tableContext, subMembers, e.Key))
                    yield return projection;
            else
            {
                var wildcardPath = e.Value as string;
                if (wildcardPath == "*" ||
                    wildcardPath?.EndsWith(".*", StringComparison.Ordinal) == true)
                {
                    var wildcardTable = wildcardPath == "*"
                        ? tableContext
                        : QueryContext.ResolveTableContext(tableContext, wildcardPath[..^2]);
                    foreach (var field in QueryContext.GetFields(wildcardTable))
                        yield return new ProjectionItem(
                            new FieldExpression(
                                wildcardTable.Alias,
                                field),
                            parentKey == null ? field.Name : $"{parentKey}.{field.Name}");
                    continue;
                }

                var sql = e.Value switch
                {
                    SqlExpression expression => expression,
                    IFieldDefinition[] expressions => QueryContext.ParseQueryExpression(tableContext, expressions),
                    IFieldDefinition expression => QueryContext.ParseQueryExpression(tableContext, expression),
                    string fieldPath => QueryContext.ParseFieldPath(tableContext, fieldPath),
                    _ => throw new NotSupportedException($"Unsupported projection value [{e.Value?.GetType().Name ?? "null"}].")
                };

                var alias = parentKey == null ? e.Key : string.Format("{0}.{1}", parentKey, e.Key);
                yield return new ProjectionItem(sql, alias);
            }
    }

}
