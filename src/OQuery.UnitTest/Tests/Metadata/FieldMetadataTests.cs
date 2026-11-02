using Microsoft.Data.Sqlite;

namespace OQuery.UnitTest.Tests.Metadata;

[TestFixture]
public sealed class FieldMetadataTests
{
    [Test]
    public void RawSqlFieldRendersWithTheCurrentTableAlias()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "OrderLine",
                [
                    new Field("Quantity", "Quantity"),
                    new Field("UnitPrice", "UnitPrice"),
                    new Field("LineTotal", "@Quantity * @UnitPrice", IsRawSql: true)
                ])
        ]);
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("OrderLine").Select("LineTotal").ToSql().CommandText;

        Assert.That(sql, Does.Contain("(t0.Quantity * t0.UnitPrice)"));
    }

    [Test]
    public void RawSqlFieldRequiresPermissionForItsLogicalField()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "OrderLine",
                [
                    new Field("Quantity", "Quantity"),
                    new Field("LineTotal", "@Quantity", IsRawSql: true)
                ])
        ],
        new PermissionManagement(
            [new MetadataPermission("OrderLine", ["Quantity"])],
            IsDenied: false));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        Assert.That(
            () => db.Query("OrderLine").Select("LineTotal").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void DbFieldRendersItsPhysicalColumnName()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("Customer", [new Field("DisplayName", "display_name")])
        ]);
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("Customer").Select("DisplayName").ToSql().CommandText;

        Assert.That(sql, Does.Contain("t0.\"display_name\""));
    }

    [Test]
    public void DbFieldUsesItsNameWhenNoPhysicalColumnNameIsSpecified()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("Customer", [new Field("DisplayName")])
        ]);
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("Customer").Select("DisplayName").ToSql().CommandText;

        Assert.That(sql, Does.Contain("t0.\"DisplayName\""));
    }
}
