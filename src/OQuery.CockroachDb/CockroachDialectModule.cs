using OQuery.Database;

namespace OQuery;

public static class CockroachDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "Cockroach",
        [],
        () => new CockroachDialect(),
        (connectionString, scriptFiles, dbVersion) => new CockroachDbInstance(connectionString, scriptFiles, dbVersion),
        ["cockroachdb"]));
}
