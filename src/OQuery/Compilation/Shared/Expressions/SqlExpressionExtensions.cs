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
            ProjectionAliasExpression => false,
            _ => false
        };

    public static bool ContainsProjectionAlias(this SqlExpression expression)
        => expression switch
        {
            ProjectionAliasExpression => true,
            FunctionSqlExpression function => function.Arguments
                .OfType<FunctionSqlExpressionArgument>()
                .Any(argument => argument.Expression.ContainsProjectionAlias()),
            BinarySqlExpression binary =>
                binary.Left.ContainsProjectionAlias() || binary.Right.ContainsProjectionAlias(),
            TemplateCompareSqlExpression compare =>
                compare.Left.ContainsProjectionAlias() || compare.Right.ContainsProjectionAlias(),
            PredicateExpression predicate =>
                predicate.Left.ContainsProjectionAlias() ||
                (predicate.Right?.ContainsProjectionAlias() ?? false),
            RangePredicateExpression range =>
                range.Left.ContainsProjectionAlias() ||
                range.Lower.ContainsProjectionAlias() ||
                range.Upper.ContainsProjectionAlias(),
            SetPredicateExpression set =>
                set.Left.ContainsProjectionAlias() ||
                set.Values.Any(value => value.ContainsProjectionAlias()),
            ProjectionExpression projection => projection.Expression.ContainsProjectionAlias(),
            _ => false
        };

    public static IEnumerable<string> ProjectionAliases(this SqlExpression expression)
        => expression switch
        {
            ProjectionAliasExpression alias => [alias.Alias],
            FunctionSqlExpression function => function.Arguments
                .OfType<FunctionSqlExpressionArgument>()
                .SelectMany(argument => argument.Expression.ProjectionAliases()),
            BinarySqlExpression binary => binary.Left.ProjectionAliases()
                .Concat(binary.Right.ProjectionAliases()),
            TemplateCompareSqlExpression compare => compare.Left.ProjectionAliases()
                .Concat(compare.Right.ProjectionAliases()),
            PredicateExpression predicate => predicate.Left.ProjectionAliases()
                .Concat(predicate.Right?.ProjectionAliases() ?? []),
            RangePredicateExpression range => range.Left.ProjectionAliases()
                .Concat(range.Lower.ProjectionAliases())
                .Concat(range.Upper.ProjectionAliases()),
            SetPredicateExpression set => set.Left.ProjectionAliases()
                .Concat(set.Values.SelectMany(value => value.ProjectionAliases())),
            ProjectionExpression projection => projection.Expression.ProjectionAliases(),
            _ => []
        };
}
