using System.Data;
using OQuery.Test.TestData;
using Npgsql;
using OQuery;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class PostgresQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetNorthStarPostgresConnection();
        }
        catch (NpgsqlException exception)
        {
            Assert.Ignore($"PostgreSQL comparison requires the Docker PostgreSQL service: {exception.Message}");
        }

        var metadata = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.NorthStarDemoData);
        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(new PostgresDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnPostgres()
    {
        var root = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");

        var requests = Directory
            .EnumerateFiles(root, "*.json")
            .Where(path => !Path.GetFileName(path).Equals(
                "Test-Check Context Calculation Fields.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Test-Multiple Contexts.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Test-Query directly on REF CONTEXT.json",
                StringComparison.Ordinal))
            .Where(path => !Path.GetFileName(path).Equals(
                "Shared Context.json",
                StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(requests, Has.Count.EqualTo(26));
        foreach (var requestPath in requests)
        {
            var query = QueryDefinitionLoader.Load(requestPath);
            Assert.That(
                () => _db.Query(query).ToList(),
                Throws.Nothing,
                Path.GetFileName(requestPath));
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
