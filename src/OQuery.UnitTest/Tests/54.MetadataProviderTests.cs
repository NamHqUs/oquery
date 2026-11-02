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

    [Test]
    public void FromYmlReportsYamlErrorLocationAndReason()
    {
        var exception = Assert.Throws<FormatException>(() => MetadataProvider.FromYml("""
            - name: Customer
              fields: [}
            """));

        Assert.That(exception!.Message, Does.Contain("YAML metadata error in '<string>'"));
        Assert.That(exception.Message, Does.Contain("line 2"));
        Assert.That(exception.Message, Does.Contain("column"));
        Assert.That(exception.Message, Does.Contain("Row 2:"));
        Assert.That(exception.Message, Does.Contain("fields: ["));
        Assert.That(exception.Message, Does.Contain("^"));
    }

    [Test]
    public void FromYmlFileReportsSourceFileName()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metadata-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(path, "- name: Customer\n  fields: [}");

            var exception = Assert.Throws<FormatException>(() => MetadataProvider.FromYmlFile(path));

            Assert.That(exception!.Message, Does.Contain($"'{path}'"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
