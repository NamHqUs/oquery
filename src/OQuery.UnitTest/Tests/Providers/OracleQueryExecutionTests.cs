using System.Data;
using OQuery.UnitTest.Setup;
using Oracle.ManagedDataAccess.Client;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
internal sealed class OracleQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetNorthStarOracleConnection();
        }
        catch (Exception exception) when (exception is OracleException or ArgumentException)
        {
            Assert.Ignore($"Oracle execution requires the Docker Oracle service: {exception.Message}");
        }

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(MetadataLoader.UnitTestData.Concat(MetadataLoader.NorthStarDemoData)),
            new FunctionProvider(new OracleDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnOracle(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }

}
