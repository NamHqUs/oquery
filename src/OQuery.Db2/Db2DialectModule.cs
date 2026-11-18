namespace OQuery;

public static class Db2DialectModule
{
    public static ISqlDialect CreateDialect() => new Db2Dialect();
}
