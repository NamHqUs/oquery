using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130
public enum RelationType
{
    Lookup = 0,
    Collection
}

[method: JsonConstructor]
public record RelationItem(string DbColumn, string RefDbColumn)
{
    public RelationItem(string dbColumn) : this(dbColumn, dbColumn)
    {
    }
}

public record Relation(
    string Name,
    string RefEntity,
    RelationType Type,
    List<RelationItem> RefKeys)
{
}
