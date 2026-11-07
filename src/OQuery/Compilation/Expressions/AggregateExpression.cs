using System.Text;
namespace OQuery.Compilation.Expressions;

class AggregateExpression : TableExpression
{
    public override Type Type => typeof(double);
    public bool GroupWithQuery { get; }
    public FunctionSqlExpression? FunctionExpression { get; }
    public SqlExpression? GroupedJoinValue { get; }
    public bool GroupedJoinCount { get; }

    public AggregateExpression(TableExpression tableContext, string aggregation, bool groupWithQuery = false) : base(tableContext)
    {
        GroupWithQuery = groupWithQuery;
        ProjectionItems = [];
        var i = aggregation.IndexOf('$');
        SqlSelect = new StringBuilder(i < 0 ? aggregation : aggregation[..i]);

        if (i > 0)
            SqlWhere.Add(new RawSqlExpression(aggregation[(i + 1)..].Replace("@", $"{tableContext.Alias}."), typeof(bool)));
    }

    public AggregateExpression(
        TableExpression tableContext,
        FunctionSqlExpression functionExpression,
        bool groupWithQuery = false,
        SqlExpression? groupedJoinValue = null,
        bool groupedJoinCount = false) : base(tableContext)
    {
        GroupWithQuery = groupWithQuery;
        FunctionExpression = functionExpression;
        GroupedJoinValue = groupedJoinValue;
        GroupedJoinCount = groupedJoinCount;
        ProjectionItems = [];
        SqlSelect = new StringBuilder();
    }

    protected override StringBuilder TranslateSelect()
        => SqlSelect ?? base.TranslateSelect();

    internal string SelectSql => SqlSelect!.ToString();
}
