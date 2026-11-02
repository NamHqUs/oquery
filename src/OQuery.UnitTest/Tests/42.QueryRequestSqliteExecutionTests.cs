using System.Data;
using OQuery;
using OQuery.Test.TestData;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestSqliteExecutionTests
{
    private static readonly string[] LegacyRequests =
    [
        "Test-Check Context Calculation Fields.json",
        "Test-Multiple Contexts.json",
        "Test-Query directly on REF CONTEXT.json",
        "Shared Context.json"
    ];

    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _connection = DefinedData.GetNorthStarSqliteDockerConnection();
        var metadata = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.NorthStarDemoData);
        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(new SqliteDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnSqlite()
    {
        var root = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests");

        var requests = Directory
            .EnumerateFiles(root, "*.json")
            .Where(path => !LegacyRequests.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(requests, Has.Count.EqualTo(26));
        foreach (var requestPath in requests)
        {
            var query = SavedQueryDefinitionLoader.Load(requestPath);
            Assert.That(
                () => _db.Query(query).ToList(),
                Throws.Nothing,
                Path.GetFileName(requestPath));
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;

}
