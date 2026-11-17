using OQuery.Database;

namespace OQuery;

public static class SqlServerDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "SqlServer",
        [],
        () => new SqlServerDialect(),
        (connectionString, scriptFiles, dbVersion) => new SqlServerDbInstance(connectionString, scriptFiles, dbVersion)));
}
