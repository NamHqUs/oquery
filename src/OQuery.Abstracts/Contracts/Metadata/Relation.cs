using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public enum RelationType
{
    Lookup = 0,
    Collection
}

public record RelationItem
{
    public RelationItem()
    {
        DbColumn = string.Empty;
        RefDbColumn = string.Empty;
    }

    [JsonConstructor]
    public RelationItem(string DbColumn, string RefDbColumn)
    {
        this.DbColumn = DbColumn;
        this.RefDbColumn = RefDbColumn;
    }

    public RelationItem(string dbColumn) : this(dbColumn, dbColumn)
    {
    }

    public string DbColumn { get; init; }
    public string RefDbColumn { get; init; }
}

public record Relation
{
    public Relation()
    {
        Name = string.Empty;
        RefEntity = string.Empty;
        RefKeys = [];
    }

    public Relation(
        string Name,
        string RefEntity,
        RelationType Type,
        List<RelationItem> RefKeys)
    {
        this.Name = Name;
        this.RefEntity = RefEntity;
        this.Type = Type;
        this.RefKeys = RefKeys;
    }

    public string Name { get; init; }
    public string RefEntity { get; init; }
    public RelationType Type { get; init; }
    public List<RelationItem> RefKeys { get; init; }
}
