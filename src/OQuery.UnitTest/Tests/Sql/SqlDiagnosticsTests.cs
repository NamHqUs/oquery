using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
namespace OQuery.UnitTest.Tests.Sql;

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
    public void CountWithoutAFieldCountsRows()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id", "Id")])
        ]);

        using var connection = new SqlConnection();
        using var db = new OQueryEngine(connection, metadata, new SqlServerDialect());

        var sql = db.Query("User").Select("COUNT()").ToSql();

        Assert.That(sql.CommandText, Does.Match(@"(?i)\bCOUNT\s*\(\s*1\s*\)"));
        Assert.That(sql.Parameters, Is.Empty);
    }

    [Test]
    public void WildcardProjectionUsesAuthorizedFields()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id", "Id"), new Field("Secret", "Secret")])
        ],
        new MetadataFieldAuthorizer(
            [new MetadataFieldPermission("User", ["Id"])],
            accessMode: AccessMode.Allow));

        using var connection = new SqlConnection();
        using var db = new OQueryEngine(connection, metadata, new SqlServerDialect());

        var sql = db.Query("User").Select("*").ToSql().CommandText;

        Assert.That(sql, Does.Contain("[Id]"));
        Assert.That(sql, Does.Not.Contain("[Secret]"));
    }

    [Test]
    public void OQueryLineBreakProjectionWithCountIfAndAnyCompilesToGroupedSql()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("Products", [new Field("Id", "Id"), new Field("Name", "Name")],
                Relations: [new Relation("Items", "Items", RelationType.Collection, [new RelationItem("Id", "ProductId")])]),
            new Entity("Items", [new Field("Id", "Id"), new Field("ProductId", "ProductId")])
        ]);

        using var connection = new SqliteConnection();
        using var db = new OQueryEngine(connection, metadata, new SqliteDialect());

        var sql = db.QueryOqr("""
            Products:
              Name
              CountIf(Id > 1 And Items.Any(Id > 0))
            """).ToSql().CommandText;

        Assert.That(sql, Does.Contain("GROUP BY t0.\"Name\""));
        Assert.That(sql, Does.Match(@"(?s)COUNT \(CASE WHEN .*AND\s+EXISTS \("));
        Assert.That(sql, Does.Contain("t1.\"ProductId\" = t0.\"Id\""));
    }

    [Test]
    public void OQueryWhereNotAnyCompilesToNotExists()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("Products", [new Field("Id", "Id"), new Field("Name", "Name")],
                Relations: [new Relation("Items", "Items", RelationType.Collection, [new RelationItem("Id", "ProductId")])]),
            new Entity("Items", [new Field("Id", "Id"), new Field("ProductId", "ProductId")])
        ]);

        using var connection = new SqliteConnection();
        using var db = new OQueryEngine(connection, metadata, new SqliteDialect());

        var sql = db.QueryOqr("""
            Products: Name
              Where Items.NotAny()
            """).ToSql().CommandText;

        Assert.That(sql, Does.Contain("NOT EXISTS"));
    }
}
