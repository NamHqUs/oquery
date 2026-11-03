namespace OQuery.QueryDefinitions;

public record YamlQueryDefinition()
{
    public string? Name { get; set; }
    public string? Id { get; set; }
    public string? Entity { get; set; }
    public List<string?>? Projection { get; set; }
    public List<List<string>>? LegacyProjectionRows { get; set; }
    public string? Filter { get; set; }
    public string? Sorts { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public bool Recursive { get; set; }
    public List<YamlQueryRelationDefinition>? Relations { get; set; }
    public List<YamlQueryRelationDefinition>? ExtraRelations { get; set; }
    public List<YamlQueryContextDefinition>? Contexts { get; set; }
}

public record YamlQueryContextDefinition
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public string? Entity { get; set; }
    public List<string?>? Projection { get; set; }
    public string? Filter { get; set; }
    public string? Sorts { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public List<YamlQueryRelationDefinition>? Relations { get; set; }
    public List<YamlQueryRelationDefinition>? ExtraRelations { get; set; }
    public YamlQueryDefinition? Query { get; set; }
    public string? Condition { get; set; }
    public QueryJoinType? JoinType { get; set; }
    public bool Recursive { get; set; }
}

public record YamlQueryRelationDefinition
{
    public string? RefContext { get; set; }
    public string? RefContextId { get; set; }
    public string? Alias { get; set; }
    public string? Condition { get; set; }
    public QueryJoinType? JoinType { get; set; }
}
