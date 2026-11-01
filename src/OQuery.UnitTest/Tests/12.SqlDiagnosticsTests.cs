using Microsoft.Data.SqlClient;

namespace OQuery.Test.Tests;

[TestFixture]
public class _12_SqlDiagnosticsTests
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
}
