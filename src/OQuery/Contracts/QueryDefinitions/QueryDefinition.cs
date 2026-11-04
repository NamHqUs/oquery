#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public enum QueryJoinType { Inner, Left, Right, Full }

public sealed record JoinDefinition(
    string RefContext,
    string? Alias = null,
    IConditionDefintion? Condition = null,
    QueryJoinType? JoinType = QueryJoinType.Left);

public record QueryDefinitionBase
{
    public string Entity { get;  }
    public List<ProjectionDefinition>? Projections { get; init; } = null;
    public ConditionDefinition? Filter { get;  init; } = null;
    public List<SortDefinition>? Sorts { get; init; } = null;
    public int? Take { get; init; } = null;
    public int? Skip { get;  init; } = null;
    public List<JoinDefinition>? Joins { get;  init; } = null;

    public QueryDefinitionBase(string entity)
    {
        Entity = entity;
    }
}

public record QueryDefinition : QueryDefinitionBase
{
    public QueryDefinition(string entity) : base(entity) { }

    public Dictionary<string, QueryDefinitionBase>? Contexts { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
