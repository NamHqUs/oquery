using Microsoft.Data.Sqlite;

namespace OQuery.Test.Tests;

[TestFixture]
public sealed class TotalRecordsTests : TestBase
{
    [Test]
    public void ToListWithTotalReturnsTotalIgnoringPaging()
    {
        var query = _db.Query("User").Skip(2).TakeQuery(2);

        var (records, resultTotal) = query.ToListWithTotalRecords();

        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(resultTotal, Is.EqualTo(6));
    }

    [Test]
    public async Task ToListWithTotalAsyncReturnsTotalIgnoringPaging()
    {
        var query = _db.Query("User").Skip(2).TakeQuery(2);

        var (records, totalRecords) = await query.ToListWithTotalRecordsAsync();

        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(totalRecords, Is.EqualTo(6));
    }
}

[TestFixture]
public sealed class BoundedTotalRecordsSqliteTests
{
    [Test]
    public async Task TotalRecordsIsBoundedAtOneThousandAndIgnoresPaging()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Probe" ("Id" INTEGER PRIMARY KEY, "Name" TEXT);
                WITH RECURSIVE seq(x) AS (
                    SELECT 1
                    UNION ALL
                    SELECT x + 1 FROM seq WHERE x < 1000
                )
                INSERT INTO "Probe" ("Id", "Name")
                SELECT x, CAST(x AS TEXT) FROM seq;
                """;
            command.ExecuteNonQuery();
        }

        using var db = new OQueryEngine(
            connection,
            [new Entity("Probe", [new Field("Id", "Id"), new Field("Name", "Name")])]);

        Assert.That(db.Query("Probe").Select("Id").TakeQuery(10).ToList(), Has.Count.EqualTo(10));

        var query = db.Query("Probe").Select("Id").Skip(500).TakeQuery(10);
        var (records, totalRecords) = query.ToListWithTotalRecords();

        Assert.That(records, Has.Count.EqualTo(10));
        Assert.That(totalRecords, Is.EqualTo(1000));

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO \"Probe\" (\"Id\", \"Name\") VALUES (1001, '1001');";
            command.ExecuteNonQuery();
        }

        var (overLimitRecords, overLimitTotal) = await query.ToListWithTotalRecordsAsync();

        Assert.That(overLimitRecords, Has.Count.EqualTo(10));
        Assert.That(overLimitTotal, Is.EqualTo(-1));
    }
}
