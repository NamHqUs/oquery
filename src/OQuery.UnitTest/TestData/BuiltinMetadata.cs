using OQuery;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OQuery.Test.TestData;

public static class BuiltinMetadata
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static IReadOnlyCollection<Entity> SaleDemoData { get; } = Load("sale-demo.yml");
    public static IReadOnlyCollection<Entity> NorthStarDemoData { get; } = Load("northstar-demo.yml");
    public static IReadOnlyCollection<Entity> UnitTestData { get; } = Load("unit-test.yml");

    private static IReadOnlyCollection<Entity> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Metadata", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Metadata fixture '{path}' was not found.", path);

        var definitions = Deserializer.Deserialize<List<EntityDefinition>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Metadata fixture '{path}' is empty.");
        return definitions.Select(definition => definition.ToEntity(path)).ToArray();
    }

    private sealed class EntityDefinition
    {
        public string? Name { get; set; }
        public string? DbSchema { get; set; }
        public string? DbTable { get; set; }
        public bool IsRawSql { get; set; }
        public List<FieldDefinition>? Fields { get; set; }
        public List<RelationDefinition>? Relations { get; set; }

        public Entity ToEntity(string path)
        {
            if (string.IsNullOrWhiteSpace(Name) || Fields is null)
                throw new InvalidDataException($"Metadata fixture '{path}' contains an invalid entity.");

            return new Entity(
                Name,
                Fields.Select(field => field.ToField(Name, path)).ToList(),
                DbSchema,
                DbTable,
                Relations?.Select(relation => relation.ToRelation(Name, path)).ToList(),
                IsRawSql);
        }
    }

    private sealed class FieldDefinition
    {
        public string? Name { get; set; }
        public string? DbColumn { get; set; }
        public DataType DataType { get; set; }
        public bool IsRawSql { get; set; }

        public Field ToField(string entityName, string path)
        {
            var dbColumn = DbColumn ?? Name;
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(dbColumn))
                throw new InvalidDataException($"Metadata entity '{entityName}' in '{path}' contains an invalid field.");
            return new Field(Name, dbColumn, DataType, IsRawSql);
        }
    }

    private sealed class RelationDefinition
    {
        public string? Name { get; set; }
        public string? RefEntity { get; set; }
        public RelationType Type { get; set; }
        public List<RelationItemDefinition>? RefKeys { get; set; }

        public Relation ToRelation(string entityName, string path)
        {
            if (string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(RefEntity) ||
                RefKeys is null ||
                RefKeys.Count == 0)
                throw new InvalidDataException($"Metadata entity '{entityName}' in '{path}' contains an invalid relation.");
            return new Relation(
                Name,
                RefEntity,
                Type,
                RefKeys.Select(key => key.ToRelationItem(entityName, path)).ToList());
        }
    }

    private sealed class RelationItemDefinition
    {
        public string? DbColumn { get; set; }
        public string? RefDbColumn { get; set; }

        public RelationItem ToRelationItem(string entityName, string path)
        {
            if (string.IsNullOrWhiteSpace(DbColumn) || string.IsNullOrWhiteSpace(RefDbColumn))
                throw new InvalidDataException($"Metadata entity '{entityName}' in '{path}' contains an invalid relation key.");
            return new RelationItem(DbColumn, RefDbColumn);
        }
    }
}
