namespace OQuery.UnitTest;

[SetUpFixture]
public sealed class DialectRegistrationSetUp
{
    [OneTimeSetUp]
    public void RegisterDialects()
    {
        SqliteDialectModule.Register();
        SqlServerDialectModule.Register();
        PostgresDialectModule.Register();
        CockroachDialectModule.Register();
        MySqlDialectModule.Register();
        OracleDialectModule.Register();
        Db2DialectModule.Register();
        FirebirdDialectModule.Register();
        SapHanaDialectModule.Register();
    }
}
