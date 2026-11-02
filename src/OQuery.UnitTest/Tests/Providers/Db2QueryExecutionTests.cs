using System.Data;
using IBM.Data.Db2;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
internal sealed class Db2QueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
#if !NET10_0_OR_GREATER
        Assert.Ignore("Db2 execution requires the net10.0 test target because the IBM Db2 native client is not available on older targets.");
#endif
        try
        {
            _connection = TestDatabase.GetNorthStarDb2Connection();
        }
        catch (Exception exception) when (exception is DB2Exception or ArgumentException)
        {
            Assert.Ignore($"Db2 execution requires the Docker Db2 service: {exception.Message}");
        }

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(MetadataLoader.UnitTestData.Concat(MetadataLoader.NorthStarDemoData)),
            new FunctionProvider(new Db2Dialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnDb2(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }
}
