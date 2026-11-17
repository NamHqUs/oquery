using OQuery.Database;

namespace OQuery;

public static class SqliteDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "Sqlite",
        ["Sqlite"],
        () => new SqliteDialect(),
        (connectionString, scriptFiles, dbVersion) => new SqliteDbInstance(connectionString, scriptFiles, dbVersion)));
}
