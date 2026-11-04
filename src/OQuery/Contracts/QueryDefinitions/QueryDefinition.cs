#pragma warning disable IDE0130
using System.Text;

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

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.Append($"Entity: {Entity} ")
        .AppendLine($"Projections: {string.Join(", ", Projections?.Select(p => p.ToText()) ?? [])}")
        .AppendLine($"Filter: {Filter?.ToText() ?? "null"}")
        .AppendLine($"Sorts: {string.Join(", ", Sorts?.Select(s => s.ToText()) ?? [])}")
        .AppendLine($"Take: {Take?.ToString() ?? "null"}")
        .AppendLine($"Skip: {Skip?.ToString() ?? "null"}");

        foreach(var join in Joins ?? [])
            sb.AppendLine($"Join: {join.JoinType} {join.RefContext} {join.Alias} ON {join.Condition?.ToText() ?? "null"}");

        return sb.ToString();
    }
}
