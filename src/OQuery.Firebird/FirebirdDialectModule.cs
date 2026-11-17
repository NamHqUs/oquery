using OQuery.Database;

namespace OQuery;

public static class FirebirdDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "Firebird",
        ["FbConnection", "Firebird"],
        () => new FirebirdDialect(),
        (connectionString, scriptFiles, dbVersion) => new FirebirdDbInstance(connectionString, scriptFiles, dbVersion)));
}
