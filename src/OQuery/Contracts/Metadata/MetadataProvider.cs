using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IMetadataProvider
{
    Entity GetEntity(string entityName);
}

public class MetadataProvider : IMetadataProvider
{
    private readonly Dictionary<string, Entity> _entities;

    public MetadataProvider(IEnumerable<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Entity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out Entity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }

    public static MetadataProvider FromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);

        var entities = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<List<YamlEntity>>(yml)
            ?? throw new FormatException("The YAML metadata cannot be null.");

        return new MetadataProvider(entities.Select(entity => new Entity(
            Required(entity.Name, "entity name"),
            (entity.Fields ?? []).Select(field => new Field(
                Required(field.Name, "field name"),
                field.DbColumn,
                field.DataType,
                field.IsRawSql)).ToList(),
            entity.DbSchema,
            entity.DbTable,
            entity.Relations?.Select(relation => new Relation(
                Required(relation.Name, "relation name"),
                Required(relation.RefEntity, "relation referenced entity"),
                relation.Type,
                (relation.RefKeys ?? []).Select(key => new RelationItem(
                    Required(key.DbColumn, "relation database column"),
                    Required(key.RefDbColumn, "relation referenced database column"))).ToList())).ToList(),
            entity.IsRawSql)));
    }

    public static MetadataProvider FromYmlFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return FromYml(File.ReadAllText(filePath));
    }

    private static string Required(string? value, string description)
        => !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"The YAML metadata requires a {description}.");

    private sealed class YamlEntity
    {
        public string? Name { get; set; }
        public string? DbSchema { get; set; }
        public string? DbTable { get; set; }
        public List<YamlField>? Fields { get; set; }
        public List<YamlRelation>? Relations { get; set; }
        public bool IsRawSql { get; set; }
    }

    private sealed class YamlField
    {
        public string? Name { get; set; }
        public string? DbColumn { get; set; }
        public DataType DataType { get; set; }
        public bool IsRawSql { get; set; }
    }

    private sealed class YamlRelation
    {
        public string? Name { get; set; }
        public string? RefEntity { get; set; }
        public RelationType Type { get; set; }
        public List<YamlRelationItem>? RefKeys { get; set; }
    }

    private sealed class YamlRelationItem
    {
        public string? DbColumn { get; set; }
        public string? RefDbColumn { get; set; }
    }
}
