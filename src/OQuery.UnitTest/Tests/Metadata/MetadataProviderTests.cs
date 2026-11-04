namespace OQuery.UnitTest.Tests.Metadata;

[TestFixture]
public sealed class MetadataProviderTests
{
    [Test]
    public void CreatesAProviderFromMetadataEntities()
    {
        var provider = new MetadataProvider(
            [new Entity(
                "Customer",
                [new Field("Id", DataType: DataType.Number)],
                DbTable: "customers")]);

        var entity = provider.GetEntity("customer");
        var fields = provider.GetFields(entity);

        Assert.Multiple(() =>
        {
            Assert.That(entity.Name, Is.EqualTo("Customer"));
            Assert.That(entity.DbTable, Is.EqualTo("customers"));
            Assert.That(fields, Has.Count.EqualTo(1));
            Assert.That(fields[0], Is.EqualTo(new Field("Id", DataType: DataType.Number)));
        });
    }

    [Test]
    public void FieldMetadataIsReturnedOnlyAfterPermissionCheck()
    {
        var entity = new Entity(
            "Customer",
            [new Field("Id"), new Field("Name")]);
        var provider = new MetadataProvider(
            [entity],
            new MetadataFieldAuthorizer(
                [new MetadataFieldPermission("Customer", ["Id"])],
                accessMode: AccessMode.Allow));

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetField(provider.GetEntity("Customer"), "Id").Name, Is.EqualTo("Id"));
            Assert.That(
                () => provider.GetField(provider.GetEntity("Customer"), "Name"),
                Throws.TypeOf<UnauthorizedAccessException>());
            Assert.That(provider.GetFields(provider.GetEntity("Customer")), Has.Count.EqualTo(1));
        });
    }

}
