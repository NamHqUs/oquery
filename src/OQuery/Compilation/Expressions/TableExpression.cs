using OQuery;
using OQuery.Compilation.Models;
using System.Text;

namespace OQuery.Compilation.Expressions;

internal sealed record JoinExpression(
    TableExpression Table,
    string JoinType,
    IReadOnlyList<SqlExpression> Conditions);

internal abstract record CommonTableExpression(string Name);

internal sealed record StandardCommonTableExpression(string Name, TableExpression Query)
    : CommonTableExpression(Name);

internal sealed record RecursiveCommonTableExpression(
    string Name,
    TableExpression Anchor,
    TableExpression Recursive,
    IReadOnlyList<string> Columns)
    : CommonTableExpression(Name);

internal sealed record OrderExpression(SqlExpression Expression, bool Descending, string? ProjectionAlias = null);

class TableExpression(IEntity entity, string alias, IReadOnlyList<Field> fields) : SqlExpression(typeof(IOQuery))
{
    public IEnumerable<ProjectionItem> ProjectionItems = [];
    public List<CommonTableExpression> CommonTableExpressions { get; } = [];
    public List<SqlExpression> SqlWhere { get; } = [];
    public List<SqlExpression> SqlHaving { get; } = [];
    public List<JoinExpression> Joins { get; } = [];
    public List<OrderExpression> Orders { get; } = [];
    public List<SqlExpression> GroupByExpressions { get; } = [];
    public StringBuilder? SqlSelect { get; protected set; }
    public StringBuilder? SqlOrder { get; set; }
    public readonly string Alias = alias;
    public readonly IEntity Entity = entity;
    private IReadOnlyList<Field> MetadataFields { get; set; } = fields;
    public string? CommonTableExpressionName { get; protected set; }
    public readonly Dictionary<string, TableExpression> JoinedTables = [];
    public TableExpression? JoinRoot { get; set; }
    protected int? TakeCount;
    protected int? SkipCount;

    internal TableExpression JoinHost => JoinRoot ?? this;
    internal override string DebugView => $"{GetType().Name}: {Entity.Name} ({Alias})";

    protected TableExpression(TableExpression source) : this(source.Entity, source.Alias, source.MetadataFields)
    {
        ProjectionItems = source.ProjectionItems.ToList();
        CommonTableExpressions.AddRange(source.CommonTableExpressions);
        SqlWhere.AddRange(source.SqlWhere);
        SqlHaving.AddRange(source.SqlHaving);
        Joins.AddRange(source.Joins);
        Orders.AddRange(source.Orders);
        GroupByExpressions.AddRange(source.GroupByExpressions);
        SqlSelect = source.SqlSelect;
        SqlOrder = source.SqlOrder;
        CommonTableExpressionName = source.CommonTableExpressionName;
        TakeCount = source.TakeCount;
        SkipCount = source.SkipCount;
        JoinRoot = source.JoinRoot;
        foreach (var pair in source.JoinedTables)
            JoinedTables[pair.Key] = pair.Value;
    }

    protected virtual StringBuilder TranslateSelect()
        => new(string.Join(',', ProjectionItems));

    internal int? Take => TakeCount;
    internal int? Skip => SkipCount;
    internal bool IsTake => this is TakeExpression && SkipCount is null;

    internal TableExpression WithoutPaging()
    {
        var copy = new TableExpression(this)
        {
            TakeCount = null,
            SkipCount = null,
        };
        return copy;
    }

    internal void EnsureDefaultProjection()
    {
        if (ProjectionItems.Any() || SqlSelect is not null)
            return;
        ProjectionItems = MetadataFields
            .Select(e => new ProjectionItem(new FieldExpression(Alias, e), e.Name))
            .ToList();
    }

    internal void SetMetadataFields(IReadOnlyList<Field> fields)
        => MetadataFields = fields;
}

internal sealed class CteTableExpression : TableExpression
{
    public string Name { get; }

    public CteTableExpression(string name, IEntity entity, string alias, IReadOnlyList<Field> fields)
        : base(entity, alias, fields)
    {
        Name = name;
        CommonTableExpressionName = name;
    }
}
