using System.Data;
using FirebirdSql.Data.FirebirdClient;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
internal sealed class FirebirdQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetNorthStarFirebirdConnection();
        }
        catch (Exception exception) when (exception is FbException or ArgumentException)
        {
            Assert.Ignore($"Firebird execution requires the Docker Firebird service: {exception.Message}");
        }

        var metadata = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.NorthStarDemoData);
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

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnFirebird(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }

}
