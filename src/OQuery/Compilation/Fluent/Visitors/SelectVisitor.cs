using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Models;
using OQuery.Compilation.Utilities;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class SelectVisitor(TableExpression tableContext, QueryCompilationResolver queryContext)
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
            TableContext.PendingProjectionAliases = dataRecord
                .Where(entry => entry.Value is not IFieldDefinition field
                    || !string.Equals(entry.Key, field.ToText(), StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.Value is not string fieldPath
                    || !string.Equals(entry.Key, fieldPath, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(entry => entry.Key, _ => typeof(object), StringComparer.OrdinalIgnoreCase);
            var selectedFields = new List<ProjectionItem>();
            foreach (var projection in BuildProjection(TableContext, dataRecord))
            {
                selectedFields.Add(projection);
                TableContext.ProjectionItems = selectedFields.ToArray();
            }
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
        var parsedMembers = OrderProjectionMembers(memebers)
            .ToDictionary(
                member => member.Key,
                member => BuildProjectionItems(tableContext, member.Value, parentKey, member.Key).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var member in memebers)
            foreach (var projection in parsedMembers[member.Key])
                yield return projection;
    }

    private IEnumerable<ProjectionItem> BuildProjectionItems(
        TableExpression tableContext,
        object? value,
        string? parentKey,
        string key)
    {
        if (value is IDictionary<string, object?> subMembers)
        {
            foreach (var projection in BuildProjection(tableContext, subMembers, key))
                yield return projection;
            yield break;
        }

        var wildcardPath = value as string;
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
            yield break;
        }

        var sql = value switch
        {
            SqlExpression expression => expression,
            IFieldDefinition[] expressions => QueryContext.ParseQueryExpression(tableContext, expressions),
            IFieldDefinition expression => QueryContext.ParseQueryExpression(tableContext, expression),
            string fieldPath => QueryContext.ParseFieldPath(tableContext, fieldPath),
            _ => throw new NotSupportedException($"Unsupported projection value [{value?.GetType().Name ?? "null"}].")
        };

        var pipelineItems = new List<ProjectionItem>();
        sql = QueryContext.ExpandAggregatePipeline(sql, pipelineItems);
        foreach (var pipelineItem in pipelineItems)
            yield return pipelineItem;

        var alias = parentKey == null ? key : string.Format("{0}.{1}", parentKey, key);
        yield return new ProjectionItem(sql, alias);
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> OrderProjectionMembers(
        IDictionary<string, object?> members)
    {
        var aliases = members.Keys.ToDictionary(key => key, key => key, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<KeyValuePair<string, object?>>(members.Count);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in members)
            Visit(member.Key);

        return ordered;

        void Visit(string key)
        {
            if (visited.Contains(key)) return;
            if (!visiting.Add(key))
                throw new FormatException($"Projection aliases cannot contain a cycle involving [{key}].");

            foreach (var dependency in GetProjectionDependencies(members[key]))
            {
                if (aliases.TryGetValue(dependency, out var dependencyKey) &&
                    !string.Equals(dependencyKey, key, StringComparison.OrdinalIgnoreCase))
                    Visit(dependencyKey);
            }

            visiting.Remove(key);
            visited.Add(key);
            ordered.Add(new KeyValuePair<string, object?>(key, members[key]));
        }
    }

    private static IEnumerable<string> GetProjectionDependencies(object? value)
    {
        switch (value)
        {
            case IFieldDefinition[] fields:
                foreach (var field in fields.SelectMany(GetProjectionDependencies))
                    yield return field;
                yield break;
            case FieldDefinition field:
                yield return field.FieldName;
                yield break;
            case FunctionFieldDefinition function:
                foreach (var argument in function.Arguments.SelectMany(GetProjectionDependencies))
                    yield return argument;
                yield break;
            case GroupFieldDefintion group:
                foreach (var field in group.Fields.SelectMany(GetProjectionDependencies))
                    yield return field;
                yield break;
        }
    }

}
