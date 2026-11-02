using Microsoft.Data.SqlClient;

namespace OQuery.Test.Tests;

[TestFixture]
public sealed class SqlDiagnosticsTests
{
    [Test]
    public void ToSqlReturnsCommandTextAndParameters()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id", "Id"), new Field("Birthday", "Birthday")])
        ]);

        using var connection = new SqlConnection();
        using var db = new OQueryEngine(connection, metadata, new SqlServerDialect());

        var query = db.Query("User")
            .Where(user => user["Birthday"] == new DateTime(2026, 1, 1));

        var sql = query.ToSql();

        Assert.That(sql.CommandText, Does.Contain("SELECT"));
        Assert.That(sql.CommandText, Does.Contain("@p0"));
        Assert.That(sql.Parameters, Has.Count.EqualTo(1));
        Assert.That(sql.Parameters[0].Name, Is.EqualTo("@p0"));
        Assert.That(sql.Parameters[0].Value, Is.EqualTo(new DateTime(2026, 1, 1)));
    }

    [Test]
    public void CountWithoutAFieldIsRejected()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id", "Id")])
        ]);

        using var connection = new SqlConnection();
        using var db = new OQueryEngine(connection, metadata, new SqlServerDialect());

        Assert.That(
            () => db.Query("User").Select("COUNT()").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>()
                .With.Message.Contain("COUNT(*)"));
    }

    [Test]
    public void WildcardProjectionUsesAuthorizedFields()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id", "Id"), new Field("Secret", "Secret")])
        ],
        new PermissionManagement(
            [new MetadataPermission("User", ["Id"])],
            IsDenied: false));

        using var connection = new SqlConnection();
        using var db = new OQueryEngine(connection, metadata, new SqlServerDialect());

        var sql = db.Query("User").Select("*").ToSql().CommandText;

        Assert.That(sql, Does.Contain("[Id]"));
        Assert.That(sql, Does.Not.Contain("[Secret]"));
    }
}
