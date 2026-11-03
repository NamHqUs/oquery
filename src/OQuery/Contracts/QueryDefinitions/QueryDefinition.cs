#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public enum QueryJoinType { Inner, Left, Right, Full }

public sealed record RelationDefinition(
    string RefContext,
    string? Alias = null,
    IConditionDefintion? Condition = null,
    QueryJoinType? JoinType = QueryJoinType.Left);

public record QueryDefinitionBase
{
    public required string Entity { get; init; }
    public List<ProjectionDefinition> Projection { get; init; } = [];
    public ConditionDefinition? Filter { get; init; }
    public List<SortDefinition> Sorts { get; init; } = [];
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public List<RelationDefinition>? Relations { get; init; } = [];
    public bool Recursive { get; set; }
}

public record QueryDefinition : QueryDefinitionBase
{
    public Dictionary<string, QueryDefinitionBase>? Contexts { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
