using OQuery;
using OQuery.Compilation.Expressions;
using OQuery.Compilation.Utilities;
using System.Linq.Expressions;

namespace OQuery.Compilation.Visitors;

class WhereVisitor(TableExpression tableContext, QueryContext queryContext)
    : LambdaVisitor(tableContext, queryContext)
{
    protected override void OnProcessResult(Expression body)
    {
        AddPredicate((SqlExpression)body);
    }

    private void AddPredicate(SqlExpression predicate)
    {
        if (predicate is BinarySqlExpression { Operator: "AND" } conjunction)
        {
            AddPredicate(conjunction.Left);
            AddPredicate(conjunction.Right);
            return;
        }

        (predicate.ContainsGroupedAggregate()
            ? TableContext.SqlHaving
            : TableContext.SqlWhere).Add(predicate);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        var leftSql = (SqlExpression)left;
        var rightSql = (SqlExpression)right;
        var oper = rightSql is ValueExpression { Value: null }
            ? node.NodeType == ExpressionType.Equal ? "IS" : "IS NOT"
            : node.NodeType.ToSql();
        return new BinarySqlExpression(leftSql, oper, rightSql, typeof(bool));
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(QueryValue) && node.Method.Name == nameof(QueryValue.Like))
        {
            var left = (SqlExpression)Visit(node.Object!);
            var value = (SqlExpression)Visit(node.Arguments[0]);
            return new PredicateExpression(left, "LIKE", value);
        }

        if (node.Method.DeclaringType == typeof(QueryValue) &&
            node.Method.Name is nameof(QueryValue.NotLike) or nameof(QueryValue.Between) or nameof(QueryValue.NotBetween))
        {
            var left = (SqlExpression)Visit(node.Object!);
            var values = node.Arguments.Select(argument => (SqlExpression)Visit(argument)).ToArray();
            return node.Method.Name switch
            {
                nameof(QueryValue.NotLike) => new PredicateExpression(left, "NOT LIKE", values[0]),
                nameof(QueryValue.Between) => new RangePredicateExpression(left, "BETWEEN", values[0], values[1]),
                nameof(QueryValue.NotBetween) => new RangePredicateExpression(left, "NOT BETWEEN", values[0], values[1]),
                _ => throw new InvalidOperationException()
            };
        }

        if (node.Method.DeclaringType == typeof(QueryValue) &&
            node.Method.Name is nameof(QueryValue.In) or nameof(QueryValue.NotIn))
        {
            var left = (SqlExpression)Visit(node.Object!);
            if (node.Arguments[0] is not NewArrayExpression values)
                throw new FormatException("Set comparison values must be an array.");

            var sqlValues = values.Expressions
                .Select(argument => argument is UnaryExpression
                    {
                        NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                    } converted
                    ? (SqlExpression)Visit(converted.Operand)
                    : (SqlExpression)Visit(argument))
                .ToArray();
            if (sqlValues.Length == 0)
                throw new FormatException("Set comparison values cannot be empty.");

            return new SetPredicateExpression(
                left,
                node.Method.Name == nameof(QueryValue.In) ? "IN" : "NOT IN",
                sqlValues);
        }

        if (node.Method.DeclaringType == typeof(QueryValue) &&
            node.Method.Name is nameof(QueryValue.IsNull) or nameof(QueryValue.IsNotNull))
        {
            var left = (SqlExpression)Visit(node.Object!);
            return new PredicateExpression(
                left,
                node.Method.Name == nameof(QueryValue.IsNull) ? "IS NULL" : "IS NOT NULL");
        }

        if (node.Method.DeclaringType == typeof(QueryValue) &&
            node.Method.Name == nameof(QueryValue.CompareTemplate))
        {
            var left = (SqlExpression)Visit(node.Object!);
            var right = (SqlExpression)Visit(node.Arguments[0]);
            var template = (string?)((ConstantExpression)node.Arguments[1]).Value
                ?? throw new InvalidOperationException("A custom comparison SQL template cannot be null.");
            return new TemplateCompareSqlExpression(left, right, template, typeof(bool));
        }

        if (node.Method.DeclaringType == typeof(QueryValue) && node.Method.Name == nameof(QueryValue.Parameter))
        {
            var value = ((ConstantExpression)node.Arguments[0]).Value;
            var parameter = QueryContext.AddDbParameter(value, true);
            return new ValueExpression(parameter, typeof(object));
        }

        if (node.Method.DeclaringType == typeof(QueryValue) &&
            node.Method.Name == nameof(QueryValue.QueryExpression))
        {
            var expression = ((ConstantExpression)node.Arguments[0]).Value as IFieldDefinition
                ?? throw new FormatException("A filter query expression cannot be null.");
            return QueryContext.ParseQueryExpression(TableContext, expression);
        }

        return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var val = node.Value == null ? null : QueryContext.AddDbParameter(node.Value);
        return new ValueExpression(val, node.Type);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if ((node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked) &&
            node.Operand is MethodCallExpression
            {
                Method.DeclaringType: not null,
                Method.Name: nameof(QueryValue.Parameter)
            } parameter)
            return Visit(parameter);

        object? val = node.Operand is ConstantExpression constant
            ? constant.Value
            : Expression.Lambda(node.Operand).Compile().DynamicInvoke(null);
        var paraName = QueryContext.AddDbParameter(val);
        return new ValueExpression(paraName, node.Type);
    }
}
