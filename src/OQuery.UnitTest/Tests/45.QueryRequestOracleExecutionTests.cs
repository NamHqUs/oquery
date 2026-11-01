using System.Data;
using OQuery.Contracts;
using OQuery.Test.TestData;
using Oracle.ManagedDataAccess.Client;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestOracleExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = DefinedData.GetNorthStarOracleConnection();
        }
        catch (OracleException exception)
        {
            Assert.Ignore($"Oracle execution requires the Docker Oracle service: {exception.Message}");
        }

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(BuiltinMetadata.UnitTestData.Concat(BuiltinMetadata.NorthStarDemoData)),
            new FunctionProvider(new OracleDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnOracle()
    {
        var root = Path.Combine(GetRepositoryRoot(), "src", "OQuery.UnitTest", "TestData", "QueryRequests");
        var requests = Directory.EnumerateFiles(root, "*.json")
            .Where(path => !new[]
            {
                "Test-Check Context Calculation Fields.json",
                "Test-Multiple Contexts.json",
                "Test-Query directly on REF CONTEXT.json",
                "Shared Context.json"
            }.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(requests, Has.Count.EqualTo(26));
        foreach (var requestPath in requests)
        {
            var query = SavedQueryDefinitionLoader.Load(requestPath);
            var fileName = Path.GetFileName(requestPath);
            Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
