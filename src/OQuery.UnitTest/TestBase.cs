using OQuery.Contracts;
using System.Data;

namespace OQuery.Test;

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
        _connection = DefinedData.GetDbConnection(DbServerType.Sqlite);

        var entities = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.SaleDemoData)
            .Concat(BuiltinMetadata.NorthStarDemoData);

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