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
public record RelationItem(string FromDbColumn, string ToDbColumn)
{
    public RelationItem(string dbCollumn) : this(dbCollumn, dbCollumn)
    {
    }
}

public record Relation(
    string Name,
    string RelatedEntity,
    RelationType Type,
    List<RelationItem> RelatedKeys)
{
}
