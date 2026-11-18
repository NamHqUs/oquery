namespace OQuery;

public static class CockroachDialectModule
{
    public static ISqlDialect CreateDialect() => new CockroachDialect();

    public static string[] ProviderAliases => ["cockroachdb"];
}
