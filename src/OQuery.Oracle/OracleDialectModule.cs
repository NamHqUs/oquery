namespace OQuery;

public static class OracleDialectModule
{
    public static ISqlDialect CreateDialect() => new OracleDialect();
}
