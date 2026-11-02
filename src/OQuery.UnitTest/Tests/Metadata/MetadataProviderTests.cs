namespace OQuery.Test.Tests;

[TestFixture]
public sealed class MetadataProviderTests
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
            new PermissionManagement(
                [new MetadataPermission("Customer", ["Id"])],
                IsDenied: false));

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetField(provider.GetEntity("Customer"), "Id").Name, Is.EqualTo("Id"));
            Assert.That(
                () => provider.GetField(provider.GetEntity("Customer"), "Name"),
                Throws.TypeOf<UnauthorizedAccessException>());
            Assert.That(provider.GetFields(provider.GetEntity("Customer")), Has.Count.EqualTo(1));
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
