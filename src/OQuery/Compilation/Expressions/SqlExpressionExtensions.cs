using OQuery;

namespace OQuery.Compilation.Expressions;

internal static class SqlExpressionExtensions
{
    public static bool ContainsGroupedAggregate(this SqlExpression expression)
        => expression switch
        {
            AggregateExpression { GroupWithQuery: true } => true,
            FunctionSqlExpression function =>
                function.Definition.IsAggregate ||
                function.Arguments
                    .OfType<FunctionSqlExpressionArgument>()
                    .Any(argument => argument.Expression.ContainsGroupedAggregate()),
            BinarySqlExpression binary =>
                binary.Left.ContainsGroupedAggregate() || binary.Right.ContainsGroupedAggregate(),
            TemplateCompareSqlExpression compare =>
                compare.Left.ContainsGroupedAggregate() || compare.Right.ContainsGroupedAggregate(),
            PredicateExpression predicate =>
                predicate.Left.ContainsGroupedAggregate() ||
                (predicate.Right?.ContainsGroupedAggregate() ?? false),
            ProjectionExpression projection => projection.Expression.ContainsGroupedAggregate(),
            _ => false
        };
}
