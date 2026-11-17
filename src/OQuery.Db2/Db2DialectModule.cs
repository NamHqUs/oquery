using OQuery.Database;

namespace OQuery;

public static class Db2DialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "Db2",
        ["Db2"],
        () => new Db2Dialect(),
        (connectionString, scriptFiles, dbVersion) => new Db2DbInstance(connectionString, scriptFiles, dbVersion)));
}
