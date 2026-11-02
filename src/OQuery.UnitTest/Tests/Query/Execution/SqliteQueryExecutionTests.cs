using System.Data;
using OQuery.UnitTest.Setup;
using OQuery.UnitTest.Tests.Providers;

namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
internal sealed class SqliteQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _connection = TestDatabase.GetNorthStarSqliteDockerConnection();
        var metadata = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.NorthStarDemoData);
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

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnSqlite(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }

}
