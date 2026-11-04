using OQuery.UnitTest.Setup;
using System.Data;

namespace OQuery.UnitTest;

[TestFixture]
public class TestBase 
{
    protected OQueryEngine _db = null!;
    protected ISqlDialect _sqlDialect = null!;
    protected IFunctionProvider _functionProvider = null!;
    private IDbConnection _connection = null!;

    [OneTimeSetUp]
    public void SetupOnce()
    {
        _connection = TestDatabase.GetDbConnection(DbServerType.Sqlite);

        var entities = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.SaleDemoData)
            .Concat(MetadataLoader.NorthStarDemoData)
            .GroupBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last());

        _sqlDialect = SqlDialect.ForConnection(_connection);
        IMetadataProvider metaProvider = new MetadataProvider(entities);
        _functionProvider = new FunctionProvider(_sqlDialect);
        _db = new OQueryEngine(_connection, metaProvider, _functionProvider, _sqlDialect);
    }


    [SetUp]
    public void Setup() { }

    [TearDown]
    public void TearDown()  { }

    [OneTimeTearDown]
    public void TearDownOnce()
    {
        _db.Dispose();
    }
}