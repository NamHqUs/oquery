using OQuery.Test.TestData;
using System.Text.Json;

namespace OQuery.Test.Tests;

[TestFixture]
public sealed class _53_MetadataLoaderTests
{
    [Test]
    public void UnitTestYamlMatchesExpectedMetadataObjects()
    {
        var expected = new[]
        {
            new Entity(
                "School",
                [new Field("Name")],
                Relations:
                [
                    new Relation("Students", "Student", RelationType.Collection, [new RelationItem("Id", "SchoolId")]),
                    new Relation("Teachers", "Teacher", RelationType.Collection, [new RelationItem("Id", "SchoolId")]),
                ]),
            new Entity(
                "User",
                [
                    new Field("Id", DataType: DataType.Number),
                    new Field("Name"),
                    new Field("ManagerId", DataType: DataType.Number),
                    new Field("Age", DataType: DataType.Number),
                    new Field("Birthday", DataType: DataType.Datetime),
                    new Field("IsMale", DataType: DataType.Boolean),
                ],
                Relations:
                [
                    new Relation("Manager", "User", RelationType.Lookup, [new RelationItem("ManagerId", "Id")]),
                    new Relation("Members", "User", RelationType.Collection, [new RelationItem("Id", "ManagerId")]),
                    new Relation("MySchool", "School", RelationType.Lookup, [new RelationItem("SchoolId", "Id")]),
                ]),
        };

        var actual = MetadataLoader.UnitTestData;

        Assert.That(ToJson(actual), Is.EqualTo(ToJson(expected)));
    }

    [Test]
    public void MetadataFixturesLoadValidEntitiesFieldsAndRelations()
    {
        var entities = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.SaleDemoData)
            .Concat(MetadataLoader.NorthStarDemoData)
            .ToArray();

        Assert.That(entities, Is.Not.Empty);
        Assert.That(
            entities.Select(entity => entity.Name),
            Is.Unique);

        foreach (var entity in entities)
        {
            Assert.That(entity.Fields, Is.Not.Empty, entity.Name);

            foreach (var relation in entity.Relations ?? [])
            {
                Assert.That(relation.RefEntity, Is.Not.Null.And.Not.Empty, entity.Name);
                Assert.That(relation.RefKeys, Is.Not.Empty, relation.Name);

                foreach (var key in relation.RefKeys)
                {
                    Assert.That(key.DbColumn, Is.Not.Null.And.Not.Empty, relation.Name);
                    Assert.That(key.RefDbColumn, Is.Not.Null.And.Not.Empty, relation.Name);
                }
            }
        }
    }

    private static string ToJson(IEnumerable<Entity> entities)
        => JsonSerializer.Serialize(
            entities.Select(entity => new
            {
                entity.Name,
                entity.DbSchema,
                entity.DbTable,
                entity.IsRawSql,
                Fields = entity.Fields.Select(field => new
                {
                    field.Name,
                    field.DbColumn,
                    field.DataType,
                    field.IsRawSql,
                }),
                Relations = entity.Relations?.Select(relation => new
                {
                    relation.Name,
                    relation.RefEntity,
                    relation.Type,
                    RefKeys = relation.RefKeys.Select(key => new
                    {
                        key.DbColumn,
                        key.RefDbColumn,
                    }),
                }),
            }));
}
