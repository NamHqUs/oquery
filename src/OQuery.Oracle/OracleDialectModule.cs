using OQuery.Database;

namespace OQuery;

public static class OracleDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "Oracle",
        ["Oracle"],
        () => new OracleDialect(),
        (connectionString, scriptFiles, dbVersion) => new OracleDbInstance(connectionString, scriptFiles, dbVersion)));
}
