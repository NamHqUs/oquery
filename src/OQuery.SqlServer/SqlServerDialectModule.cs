namespace OQuery;

public static class SqlServerDialectModule
{
    public static ISqlDialect CreateDialect() => new SqlServerDialect();
}
