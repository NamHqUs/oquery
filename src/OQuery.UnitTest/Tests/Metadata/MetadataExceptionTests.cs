namespace OQuery.UnitTest.Tests.Metadata;

[TestFixture]
public sealed class MetadataExceptionTests
{
    [Test]
    public void GetUnknownEntityThrowsEntityNotFoundException()
    {
        var provider = new MetadataProvider([]);

        var exception = Assert.Throws<EntityNotFoundException>(() => provider.GetEntity("User"));

        Assert.That(exception!.EntityName, Is.EqualTo("User"));
        Assert.That(exception.Message, Is.EqualTo("Entity [User] was not found."));
    }

    [Test]
    public void GetUnknownFieldThrowsFieldNotFoundException()
    {
        var entity = new Entity("User", []);

        var exception = Assert.Throws<FieldNotFoundException>(() => entity.GetField("Name"));

        Assert.That(exception!.EntityName, Is.EqualTo("User"));
        Assert.That(exception.FieldName, Is.EqualTo("Name"));
        Assert.That(exception.Message, Is.EqualTo("Field [Name] was not found in entity [User]."));
    }

    [Test]
    public void GetUnknownRelationThrowsRelationNotFoundException()
    {
        var entity = new Entity("User", []);

        var exception = Assert.Throws<RelationNotFoundException>(() => entity.GetRelation("Manager"));

        Assert.That(exception!.EntityName, Is.EqualTo("User"));
        Assert.That(exception.RelationName, Is.EqualTo("Manager"));
        Assert.That(exception.Message, Is.EqualTo("Relation [Manager] was not found in entity [User]."));
    }
}
