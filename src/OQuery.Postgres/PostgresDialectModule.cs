namespace OQuery;

public static class PostgresDialectModule
{
    public static ISqlDialect CreateDialect() => new PostgresDialect();

    public static string[] ProviderAliases => ["postgresql"];
}
