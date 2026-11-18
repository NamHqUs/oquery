namespace OQuery;

public static class SapHanaDialectModule
{
    public static ISqlDialect CreateDialect() => new SapHanaDialect();

    public static string[] ProviderAliases => ["hana"];
}
