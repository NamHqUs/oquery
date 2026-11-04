namespace OQuery.UnitTest.Tests.Metadata;

[TestFixture]
public sealed class MetadataFieldPermissionTests
{
    [Test]
    public void CreatesMetadataFieldAuthorizer()
    {
        var permissions = new MetadataFieldAuthorizer(
            [
                new MetadataFieldPermission("User", ["Id", "DisplayName"]),
                new MetadataFieldPermission("AuditLog", null),
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(permissions.CanReadField("user", "id"), Is.False);
            Assert.That(permissions.CanReadField("User", "DisplayName"), Is.False);
            Assert.That(permissions.CanReadField("AuditLog", "CreatedAt"), Is.False);
            Assert.That(permissions.CanReadField("User", "Email"), Is.True);
        });
    }

    [Test]
    public void ConstructorUsesAllowModeWhenRequested()
    {
        var permissions = new MetadataFieldAuthorizer(
            [new MetadataFieldPermission("User", ["Id"])],
            accessMode: AccessMode.Allow);

        Assert.Multiple(() =>
        {
            Assert.That(permissions.CanReadField("User", "Id"), Is.True);
            Assert.That(permissions.CanReadField("User", "Email"), Is.False);
        });
    }

}
