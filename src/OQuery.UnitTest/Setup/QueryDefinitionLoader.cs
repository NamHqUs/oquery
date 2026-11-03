using System.Text.Json;
using System.Text.Json.Serialization;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Setup;

internal static class QueryDefinitionLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
        JsonQueryDefinitionParser.CreateSerializerOptions();

    public static QueryDefinition Load(string path)
    {
        var request = JsonSerializer.Deserialize<StoredQueryRequest>(
            File.ReadAllText(path),
            JsonOptions)
            ?? throw new InvalidDataException($"Query request '{path}' is empty.");
        var contexts = request.Contexts.ToDictionary(context => context.Id, StringComparer.OrdinalIgnoreCase);
        return BuildQuery(request.MainQuery, contexts, [], allowDirectContext: true);
    }
    private static QueryDefinition BuildQuery(
        StoredQueryNode node,
        IReadOnlyDictionary<string, StoredQueryNode> contexts,
        HashSet<string> visited,
        bool allowDirectContext)
    {
        if (!visited.Add(node.Id))
            throw new InvalidDataException($"Query request context [{node.Name}] is recursive.");

        var query = new QueryDefinition
        {
            Entity = node.Entity,
            Projection = node.Projection,
            Filter = node.Filters,
            Sorts = node.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
            Page = node.Page,
            PageSize = node.PageSize,
            Contexts = BuildContexts(node, contexts, visited, allowDirectContext)
        };
        if (node.ExtraRelations is { Count: > 0 })
            query.Relations.AddRange(node.ExtraRelations.Select(relation =>
                new RelationDefinition(
                    contexts[relation.RefContextId].Name,
                    relation.Alias,
                    relation.Condition,
                    relation.JoinType)));
        else
            query.Relations.AddRange(query.Contexts.Keys.Select(name => new RelationDefinition(name)));
        return query;
    }

    private static Dictionary<string, QueryDefinitionBase> BuildContexts(
        StoredQueryNode node,
        IReadOnlyDictionary<string, StoredQueryNode> contexts,
        HashSet<string> visited,
        bool allowDirectContext)
    {
        if (node.ExtraRelations is { Count: > 0 })
        {
            var result = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase);
            foreach (var relation in node.ExtraRelations)
            {
                if (!contexts.TryGetValue(relation.RefContextId, out var context))
                    throw new InvalidDataException($"Unknown query context [{relation.RefContextId}].");
                result[context.Name] = BuildQuery(
                        context,
                        contexts,
                        new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase),
                        allowDirectContext: false);
            }
            return result;
        }

        var directContext = contexts.Values.FirstOrDefault(context =>
            string.Equals(context.Name, node.Entity, StringComparison.OrdinalIgnoreCase));
        if (!allowDirectContext ||
            directContext is null ||
            string.Equals(directContext.Id, node.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
        {
            [directContext.Name] = BuildQuery(
                    directContext,
                    contexts,
                    new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase),
                    allowDirectContext: false)
        };
    }

    private sealed class StoredQueryRequest
    {
        public required StoredQueryNode MainQuery { get; init; }
        public List<StoredQueryNode> Contexts { get; init; } = [];
    }

    private sealed class StoredQueryNode
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Entity { get; init; }
        public List<ProjectionDefinition> Projection { get; init; } = [];
        public ConditionDefinition? Filters { get; init; }
        public List<List<SortDefinition>>? Sorts { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public List<StoredRelation>? ExtraRelations { get; init; }
    }

    private sealed class StoredRelation
    {
        public required string RefContextId { get; init; }
        public required string Alias { get; init; }
        public ConditionDefinition? Condition { get; init; }
        public QueryJoinType JoinType { get; init; }
    }
}
