namespace OQuery.UnitTest.Tests.Metadata;

[TestFixture]
public sealed class MetadataFieldPermissionTests
{
    [Test]
    public void FromYmlCreatesMetadataFieldAuthorizer()
    {
        var permissions = MetadataFieldAuthorizer.FromYml("""
            - entityName: User
              fields:
              - Id
              - DisplayName
            - entityName: AuditLog
            """);

        Assert.Multiple(() =>
        {
            Assert.That(permissions.CanReadField("user", "id"), Is.False);
            Assert.That(permissions.CanReadField("User", "DisplayName"), Is.False);
            Assert.That(permissions.CanReadField("AuditLog", "CreatedAt"), Is.False);
            Assert.That(permissions.CanReadField("User", "Email"), Is.True);
        });
    }

    [Test]
    public void ConstructorWithYmlUsesAllowModeWhenRequested()
    {
        var permissions = new MetadataFieldAuthorizer(
            """
            - entityName: User
              fields:
              - Id
            """,
            accessMode: AccessMode.Allow);

        Assert.Multiple(() =>
        {
            Assert.That(permissions.CanReadField("User", "Id"), Is.True);
            Assert.That(permissions.CanReadField("User", "Email"), Is.False);
        });
    }

    [Test]
    public void FromYmlRejectsNullYaml()
    {
        Assert.That(
            () => MetadataFieldAuthorizer.FromYml(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void FromYmlFileCreatesMetadataFieldAuthorizer()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"permissions-{Guid.NewGuid():N}.yml");

        try
        {
            File.WriteAllText(path, """
                - entityName: User
                  fields:
                  - Id
                """);

            var permissions = MetadataFieldAuthorizer.FromYmlFile(path);

            Assert.That(permissions.CanReadField("User", "Id"), Is.False);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void FromYmlFileRejectsNullPath()
    {
        Assert.That(
            () => MetadataFieldAuthorizer.FromYmlFile(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void FromYmlReportsYamlError()
    {
        var exception = Assert.Throws<FormatException>(() =>
            MetadataFieldAuthorizer.FromYml("""
                - entityName: User
                  fields: [}
                """));

        Assert.That(exception!.Message, Does.Contain("YAML permission error"));
    }
}
