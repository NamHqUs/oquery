using System.Text;
using OQuery.Compilation.Models;
namespace OQuery.Compilation.Expressions;

class AggregateExpression : TableExpression
{
    public override Type Type => typeof(double);
    public bool GroupWithQuery { get; }
    public FunctionSqlExpression? FunctionExpression { get; }

    public AggregateExpression(TableExpression tableContext, string aggregation, bool groupWithQuery = false) : base(tableContext)
    {
        GroupWithQuery = groupWithQuery;
        ProjectionItems = HasProjectionAlias(tableContext)
            ? tableContext.ProjectionItems.ToList()
            : [];
        var i = aggregation.IndexOf('$');
        SqlSelect = new StringBuilder(i < 0 ? aggregation : aggregation[..i]);

        if (i > 0)
            SqlWhere.Add(new RawSqlExpression(aggregation[(i + 1)..].Replace("@", $"{tableContext.Alias}."), typeof(bool)));
    }

    public AggregateExpression(
        TableExpression tableContext,
        FunctionSqlExpression functionExpression,
        bool groupWithQuery = false) : base(tableContext)
    {
        GroupWithQuery = groupWithQuery;
        FunctionExpression = functionExpression;
        ProjectionItems = HasProjectionAlias(tableContext)
            ? tableContext.ProjectionItems.ToList()
            : [];
        if (ProjectionItems.Any() && functionExpression.Arguments
            .OfType<FunctionSqlExpressionArgument>()
            .Select(argument => argument.Expression)
            .OfType<FieldExpression>()
            .FirstOrDefault() is { } field &&
            ProjectionItems.All(item => !string.Equals(item.Alias, field.MetaField.Name, StringComparison.OrdinalIgnoreCase)))
        {
            ProjectionItems = ProjectionItems
                .Append(new ProjectionItem(field))
                .ToList();
        }
        SqlSelect = new StringBuilder();
    }

    private static bool HasProjectionAlias(TableExpression tableContext)
        => tableContext.SqlWhere.Concat(tableContext.SqlHaving)
            .Any(expression => expression.ContainsProjectionAlias());

    protected override StringBuilder TranslateSelect()
        => SqlSelect ?? base.TranslateSelect();

    internal string SelectSql => SqlSelect!.ToString();
}
