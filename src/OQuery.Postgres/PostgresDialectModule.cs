using OQuery.Database;

namespace OQuery;

public static class PostgresDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "Postgres",
        ["Npgsql"],
        () => new PostgresDialect(),
        (connectionString, scriptFiles, dbVersion) => new PostgresDbInstance(connectionString, scriptFiles, dbVersion),
        ["postgresql"]));
}
