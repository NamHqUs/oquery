using System.Data;
using System.Globalization;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public static class SqlDialect
{
    internal static string FormatParameterValue(object? value)
        => value switch
        {
            null => "NULL",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => $"'{dateTime.ToString("O", CultureInfo.InvariantCulture)}'",
            DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
            _ => $"'{value}'"
        };

    public static ISqlDialect ForConnection(IDbConnection connection)
    {
        var connectionType = connection.GetType().FullName;

        if (connectionType?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            return new SqliteDialect();

        if (connectionType?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            return new PostgresDialect();

        if (connectionType?.Contains("FbConnection", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Firebird", StringComparison.OrdinalIgnoreCase) == true)
            return new FirebirdDialect();

        if (connectionType?.Contains("OracleConnection", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true)
            return new OracleDialect();

        if (connectionType?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Maria", StringComparison.OrdinalIgnoreCase) == true)
            return new MariaDialect();

        if (connectionType?.Contains("Hana", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("Hdb", StringComparison.OrdinalIgnoreCase) == true)
            return new SapHanaDialect();

        if (connectionType?.Contains("Db2", StringComparison.OrdinalIgnoreCase) == true ||
            connectionType?.Contains("DB2", StringComparison.OrdinalIgnoreCase) == true)
            return new Db2Dialect();

        return new SqlServerDialect();
    }

    public static ISqlDialect ForProvider(string? provider, IDbConnection? connection = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return connection is null
                ? throw new ArgumentException("A database provider is required.", nameof(provider))
                : ForConnection(connection);

        return provider.Trim().ToLowerInvariant() switch
        {
            "sqlite" => new SqliteDialect(),
            "sqlserver" => new SqlServerDialect(),
            "postgres" or "postgresql" => new PostgresDialect(),
            "cockroach" or "cockroachdb" => new CockroachDialect(),
            "firebird" => new FirebirdDialect(),
            "maria" or "mariadb" or "mysql" => new MariaDialect(),
            "oracle" => new OracleDialect(),
            "saphana" or "hana" => new SapHanaDialect(),
            "db2" => new Db2Dialect(),
            _ => throw new ArgumentException($"Unsupported database provider '{provider}'.", nameof(provider))
        };
    }
}
