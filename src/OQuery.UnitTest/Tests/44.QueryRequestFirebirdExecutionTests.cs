using System.Data;
using FirebirdSql.Data.FirebirdClient;
using OQuery;
using OQuery.Test.TestData;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestFirebirdExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = DefinedData.GetNorthStarFirebirdConnection();
        }
        catch (FbException exception)
        {
            Assert.Ignore($"Firebird execution requires the Docker Firebird service: {exception.Message}");
        }

        var metadata = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.NorthStarDemoData);
        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(new FirebirdDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [Test]
    public void AllNormalSavedQueryDefinitionsExecuteOnFirebird()
    {
        var root = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests");

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
