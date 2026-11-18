namespace OQuery;

public static class SqliteDialectModule
{
    public static ISqlDialect CreateDialect() => new SqliteDialect();
}
