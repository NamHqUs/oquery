namespace OQuery.Test.Tests;

[TestFixture]
public sealed class _54_MetadataProviderTests
{
    [Test]
    public void FromYmlCreatesAProviderFromMetadataEntities()
    {
        var provider = MetadataProvider.FromYml("""
            - name: Customer
              dbTable: customers
              fields:
              - name: Id
                dataType: Number
            """);

        var entity = provider.GetEntity("customer");

        Assert.Multiple(() =>
        {
            Assert.That(entity.Name, Is.EqualTo("Customer"));
            Assert.That(entity.DbTable, Is.EqualTo("customers"));
            Assert.That(entity.Fields, Has.Count.EqualTo(1));
            Assert.That(entity.Fields[0], Is.EqualTo(new Field("Id", DataType: DataType.Number)));
        });
    }

    [Test]
    public void FromYmlRejectsNullYaml()
    {
        Assert.That(
            () => MetadataProvider.FromYml((string)null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void FromYmlFileRejectsNullPath()
    {
        Assert.That(
            () => MetadataProvider.FromYmlFile(null!),
            Throws.TypeOf<ArgumentNullException>());
    }
}
