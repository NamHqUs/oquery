using System.Data;
using OQuery.Database;

namespace OQuery;

public sealed record SqlDialectRegistration(
    string Name,
    IReadOnlyList<string> ConnectionTypeMarkers,
    Func<ISqlDialect> CreateDialect,
    Func<string, string[], string, DbInstance>? CreateDbInstance = null,
    IReadOnlyList<string>? Aliases = null);

public static class SqlDialectRegistry
{
    private static readonly object Gate = new();
    private static readonly List<SqlDialectRegistration> Registrations = [];

    public static void Register(SqlDialectRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (Gate)
        {
            Registrations.RemoveAll(existing => string.Equals(existing.Name, registration.Name, StringComparison.OrdinalIgnoreCase));
            Registrations.Add(registration);
        }
    }

    public static IReadOnlyList<string> RegisteredNames
    {
        get
        {
            lock (Gate)
                return Registrations.Select(registration => registration.Name).ToArray();
        }
    }

    public static IReadOnlyList<ISqlDialect> CreateAllDialects()
    {
        SqlDialectRegistration[] snapshot;
        lock (Gate)
            snapshot = Registrations.ToArray();
        return snapshot.Select(registration => registration.CreateDialect()).ToArray();
    }

    public static ISqlDialect CreateForProvider(string provider)
    {
        var registration = FindByProvider(provider)
            ?? throw new ArgumentException(
                $"Unsupported database provider '{provider}'. Register its OQuery dialect module before use.",
                nameof(provider));
        return registration.CreateDialect();
    }

    public static ISqlDialect CreateForConnection(IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var connectionType = connection.GetType().FullName ?? string.Empty;
        SqlDialectRegistration[] snapshot;
        lock (Gate)
            snapshot = Registrations.ToArray();

        var match = snapshot.FirstOrDefault(registration =>
            registration.ConnectionTypeMarkers.Any(marker =>
                connectionType.Contains(marker, StringComparison.OrdinalIgnoreCase)));
        match ??= snapshot.FirstOrDefault(registration =>
            string.Equals(registration.Name, "SqlServer", StringComparison.OrdinalIgnoreCase));
        return (match ?? throw new InvalidOperationException(
            $"No OQuery dialect is registered for connection type '{connectionType}'."))
            .CreateDialect();
    }

    public static DbInstance CreateDbInstance(string provider, string connectionString, string[] scriptFiles, string dbVersion)
    {
        var registration = FindByProvider(provider)
            ?? throw new ArgumentException($"Unsupported database '{provider}'.", nameof(provider));
        return (registration.CreateDbInstance ?? throw new ArgumentException(
                $"Database '{provider}' does not support provisioning.", nameof(provider)))
            (connectionString, scriptFiles, dbVersion);
    }

    private static SqlDialectRegistration? FindByProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return null;

        SqlDialectRegistration[] snapshot;
        lock (Gate)
            snapshot = Registrations.ToArray();

        return snapshot.FirstOrDefault(registration =>
            string.Equals(registration.Name, provider.Trim(), StringComparison.OrdinalIgnoreCase)
            || (registration.Aliases?.Any(alias => string.Equals(alias, provider.Trim(), StringComparison.OrdinalIgnoreCase)) ?? false));
    }
}
