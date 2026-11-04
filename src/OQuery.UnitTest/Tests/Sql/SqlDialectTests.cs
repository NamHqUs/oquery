using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Tests.Sql;

[TestFixture]
public sealed class SqlDialectTests
{
    private static readonly IMetadataProvider Metadata = new MetadataProvider(
    [
        new Entity("User", [new Field("Id", "Id"), new Field("Name", "Name")])
    ]);

    [Test]
    public void SqlServerDialectUsesTopAndBracketIdentifiers()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var db = new OQueryEngine(connection, Metadata, new SqlServerDialect());

        Assert.That(
            new SqlServerDialect().RenderTable((Entity)Metadata.GetEntity("User")),
            Is.EqualTo("[dbo].[User]"));

        var sql = db.Query("User").Select("Id").ToString();

        Assert.That(sql, Does.Contain("SELECT t0.[Id]"));
        Assert.That(sql, Does.Contain("FROM [dbo].[User] t0"));
        Assert.That(new SqlServerDialect().RenderTakePrefix(2), Is.EqualTo("TOP 2 "));
    }

    [Test]
    public void SqlServerDialectAddsRequiredOrderByForPagingWithoutSort()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var db = new OQueryEngine(connection, Metadata, new SqlServerDialect());

        var sql = db.Query("User").Select("Id").Skip(0).TakeQuery(2).ToSql().CommandText;

        Assert.That(sql, Does.Match(@"ORDER BY\s*\(\s*SELECT\s+1\s*\)"));
        Assert.That(sql.IndexOf("ORDER BY", StringComparison.Ordinal),
            Is.LessThan(sql.IndexOf("OFFSET 0 ROWS", StringComparison.Ordinal)));
    }

    [Test]
    public void SqlServerDialectDoesNotAddSyntheticOrderByWhenSorted()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var db = new OQueryEngine(connection, Metadata, new SqlServerDialect());

        var sql = db.Query("User")
            .Select("Id")
            .OrderBy("Id")
            .Skip(0)
            .TakeQuery(2)
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("ORDER BY [Id] ASC"));
        Assert.That(sql, Does.Not.Contain("ORDER BY (SELECT 1)"));
    }

    [Test]
    public void Db2DialectSortsProjectedAliasesByProjectionOrdinal()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var db = new OQueryEngine(connection, Metadata, new Db2Dialect());
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name | DisplayName"),
            Sorts = [new SortDefinition(new FieldDefinition("DisplayName"), QuerySortDirection.Descending)]
        };

        var sql = db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain("ORDER BY 1 DESC"));
        Assert.That(sql, Does.Not.Contain("ORDER BY \"DISPLAYNAME\""));
    }

    [Test]
    public void SqlServerDialectRendersTypedParameterDeclarations()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection();
        using var db = new OQueryEngine(connection, Metadata, new SqlServerDialect());
        var name = "O'Neil";

        var sql = db.Query("User")
            .Where(user => user["Name"] == name)
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("DECLARE @p0 nvarchar(4000) = 'O''Neil';"));
        Assert.That(new SqlServerDialect().RenderQueryWithParameters(
            "SELECT @p0",
            [("@p0", (object?)1)]), Does.StartWith("DECLARE @p0 int = 1;"));
    }

    [Test]
    public void SqliteDialectRendersParameterValuesAsDiagnostics()
    {
        using var connection = new SqliteConnection();
        using var db = new OQueryEngine(connection, Metadata, new SqliteDialect());

        var sql = db.Query("User")
            .Where(user => user["Name"] == "O'Neil")
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("-- Parameters: @p0 = 'O''Neil'"));
    }

    [Test]
    public void EntityUsesLogicalNameAsDefaultPhysicalTableName()
    {
        var entity = new Entity("User", []);

        Assert.That(entity.Name, Is.EqualTo("User"));
        Assert.That(entity.DbTable, Is.EqualTo("User"));
        Assert.That(entity.IsRawSql, Is.False);
    }

    [Test]
    public void RawSqlEntityFlagPreservesItsLogicalName()
    {
        var entity = new Entity(Name: "Users", Fields: [], IsRawSql: true, DbTable: "Users");

        Assert.That(entity.Name, Is.EqualTo("Users"));
        Assert.That(entity.DbTable, Is.EqualTo("Users"));
    }

    [Test]
    public void RawSqlEntityFlagAllowsSelectSourcesWithBlacklistedWordsInLiteralsAndIdentifiers()
    {
        var sqlServerEntity = new Entity(
            Name: "Users",
            Fields: [],
            IsRawSql: true,
            DbTable: "(SELECT N'delete update' AS [delete] FROM [Update])");
        var sqliteEntity = new Entity(
            Name: "Users",
            Fields: [],
            IsRawSql: true,
            DbTable: "(SELECT 'delete update' AS `delete` FROM `Update`)");
        var postgresEntity = new Entity(
            Name: "Users",
            Fields: [],
            IsRawSql: true,
            DbTable: "(SELECT $value$delete update into$value$ AS \"delete\" FROM \"Update\")");

        Assert.That(new SqlServerDialect().RenderTable(sqlServerEntity), Is.EqualTo(sqlServerEntity.DbTable));
        Assert.That(new SqliteDialect().RenderTable(sqliteEntity), Is.EqualTo(sqliteEntity.DbTable));
        Assert.That(new PostgresDialect().RenderTable(postgresEntity), Is.EqualTo(postgresEntity.DbTable));
    }

    [Test]
    public void RawSqlEntityFlagRejectsBlacklistedKeywordsInTheSource()
    {
        var sqlServerEntity = new Entity(
            Name: "Users",
            Fields: [],
            IsRawSql: true,
            DbTable: "(SELECT Id INTO NewUsers FROM Users)");
        var sqliteEntity = new Entity(
            Name: "Users",
            Fields: [],
            IsRawSql: true,
            DbTable: "(SELECT Id FROM Users; DELETE FROM Users)");
        var postgresEntity = new Entity(
            Name: "Users",
            Fields: [],
            IsRawSql: true,
            DbTable: "(WITH removed AS (DELETE FROM Users RETURNING Id) SELECT Id FROM removed)");

        Assert.Throws<ArgumentException>(() => new SqlServerDialect().RenderTable(sqlServerEntity));
        Assert.Throws<ArgumentException>(() => new SqliteDialect().RenderTable(sqliteEntity));
        Assert.Throws<ArgumentException>(() => new PostgresDialect().RenderTable(postgresEntity));
    }

    [Test]
    public void RawSqlEntityGuardDoesNotRejectRegularEntityTableNames()
    {
        var entity = new Entity("User", "dbo", "Update", []);

        Assert.That(new SqlServerDialect().RenderTable(entity), Is.EqualTo("[dbo].[Update]"));
    }

    [Test]
    public void SqlDialectValidatesTheEntireStatement()
    {
        Assert.That(
            () => new SqliteDialect().ValidateSqlStatement(
                "SELECT Id FROM Users; DELETE FROM Users"),
            Throws.ArgumentException);
    }

    [Test]
    public void SqlDialectValidatesGeneratedStatementsBeforeExecution()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [
                    new Field("Id", "Id"),
                    new Field("Injected", "Id, DELETE FROM User", IsRawSql: true)
                ])
        ]);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE \"User\" (\"Id\" INTEGER);";
            command.ExecuteNonQuery();
        }

        using var db = new OQueryEngine(connection, metadata, new SqliteDialect());

        Assert.That(
            () => db.Query("User").Select("Injected").ToList(),
            Throws.ArgumentException);
    }

    [Test]
    public void EntityKeepsLogicalAndPhysicalNamesIndependentWhenBothAreProvided()
    {
        var entity = new Entity(Name: "Student", Fields: [], IsRawSql: true, DbTable: "[User]");

        Assert.That(entity.Name, Is.EqualTo("Student"));
        Assert.That(entity.DbTable, Is.EqualTo("[User]"));
    }

    [Test]
    public void EntityRendersConfiguredSchemaAndTable()
    {
        var entity = new Entity("Student", "school", "students", []);

        Assert.That(new SqlServerDialect().RenderTable(entity), Is.EqualTo("[school].[students]"));
        Assert.That(new PostgresDialect().RenderTable(entity), Is.EqualTo("\"school\".\"students\""));
        Assert.That(
            new SqlServerDialect().RenderTable(new Entity("Claim", "in", "Claims", [])),
            Is.EqualTo("[in].[Claims]"));
    }

    [Test]
    public void SqliteDialectUsesLimitOffsetAndQuotedIdentifiers()
    {
        using var connection = new SqliteConnection();
        connection.ConnectionString = "Data Source=:memory:";
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE \"User\" (\"Id\" INTEGER, \"Name\" TEXT);";
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO \"User\" (\"Id\", \"Name\") VALUES (1, 'Alice'), (2, 'Bob'), (3, 'Charlie');";
            command.ExecuteNonQuery();
        }

        using var db = new OQueryEngine(connection, Metadata, new SqliteDialect());
        var data = db.Query("User").Select("Id").Skip(1).ToList();

        Assert.That(data.Select(e => e["Id"]).ToArray(), Is.EqualTo(new object?[] { 2L, 3L }));
        Assert.That(new SqliteDialect().RenderPaging(3, 2), Is.EqualTo(" LIMIT 2 OFFSET 3"));
    }
}
