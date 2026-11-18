namespace OQuery;

public static class FirebirdDialectModule
{
    public static ISqlDialect CreateDialect() => new FirebirdDialect();
}
