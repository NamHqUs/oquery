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
        => SqlDialectRegistry.CreateForConnection(connection);

    public static ISqlDialect ForProvider(string? provider, IDbConnection? connection = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return connection is null
                ? throw new ArgumentException("A database provider is required.", nameof(provider))
                : ForConnection(connection);

        return SqlDialectRegistry.CreateForProvider(provider);
    }
}
