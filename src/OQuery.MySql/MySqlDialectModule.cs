using OQuery.Database;

namespace OQuery;

public static class MySqlDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "MySql",
        ["MySql", "Maria"],
        () => new MariaDialect(),
        (connectionString, scriptFiles, dbVersion) => new MySqlDbInstance(connectionString, scriptFiles, dbVersion),
        ["maria", "mariadb"]));
}
