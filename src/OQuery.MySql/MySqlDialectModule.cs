namespace OQuery;

public static class MySqlDialectModule
{
    public static ISqlDialect CreateDialect() => new MariaDialect();

    public static string[] ProviderAliases => ["maria", "mariadb"];
}
