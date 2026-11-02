namespace OQuery.Test.Tests;

[TestFixture]
public sealed class _55_MetadataPermissionTests
{
    [Test]
    public void FromYmlCreatesPermissionManagement()
    {
        var permissions = PermissionManagement.FromYml("""
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
    public void ConstructorWithYmlUsesAllowListWhenRequested()
    {
        var permissions = new PermissionManagement(
            """
            - entityName: User
              fields:
              - Id
            """,
            IsDenied: false);

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
            () => PermissionManagement.FromYml(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void FromYmlFileCreatesPermissionManagement()
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

            var permissions = PermissionManagement.FromYmlFile(path);

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
            () => PermissionManagement.FromYmlFile(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void FromYmlReportsYamlError()
    {
        var exception = Assert.Throws<FormatException>(() =>
            PermissionManagement.FromYml("""
                - entityName: User
                  fields: [}
                """));

        Assert.That(exception!.Message, Does.Contain("YAML permission error"));
    }
}
