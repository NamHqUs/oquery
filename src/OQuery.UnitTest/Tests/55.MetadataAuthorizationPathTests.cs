using Microsoft.Data.Sqlite;

namespace OQuery.Test.Tests;

[TestFixture]
public sealed class _55_MetadataAuthorizationPathTests
{
    [Test]
    public void JoinedProjectionRequiresPermissionForTheJoinedField()
    {
        using var db = CreateDatabase();

        Assert.That(
            () => db.Query("User").Select("Name", "Members.Secret").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void JoinedAggregateRequiresPermissionForItsField()
    {
        using var db = CreateDatabase();

        Assert.That(
            () => db.Query("User").Select("Name", "Members.COUNT(Secret)").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void ProjectionAliasDoesNotGrantAccessToAHiddenPhysicalField()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id"), new Field("Secret")])
        ],
        new PermissionManagement(
            [new MetadataPermission("User", ["Id"])],
            IsDenied: false));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("User")
            .Select(new Dictionary<string, object?> { ["Secret"] = "Id" })
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("Id"));
        Assert.That(sql, Does.Not.Contain("t0.\"Secret\""));
    }

    private static OQueryEngine CreateDatabase()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [new Field("Id"), new Field("Name")],
                Relations:
                [
                    new Relation(
                        "Members",
                        "Member",
                        RelationType.Collection,
                        [new RelationItem("Id", "UserId")])
                ]),
            new Entity("Member", [new Field("Id"), new Field("UserId"), new Field("Secret")])
        ],
        new PermissionManagement(
        [
            new MetadataPermission("User", ["Id", "Name"]),
            new MetadataPermission("Member", ["Id"])
        ],
        IsDenied: false));

        return new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
    }
}
