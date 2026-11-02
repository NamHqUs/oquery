using Microsoft.Data.Sqlite;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public sealed class SqliteDbInstance : DbInstance, IDisposable
{

    private static readonly Regex MemoryDatabaseAttachment = new(
        """ATTACH\s+DATABASE\s+':memory:'\s+AS\s+(?<schema>"(?:""|[^"])+"|[A-Za-z_][A-Za-z0-9_]*)\s*;""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly object _keepAliveLock = new();

    private bool? _inMemory;
    private bool InMemory => _inMemory ??= ConnectionString.Contains(":memory:");
    private SqliteConnection? _keepAliveConnection;

    public SqliteDbInstance(string connectionString, string[] scriptFiles, string dbVersion)
        : base( NormalizeConnectionString(connectionString), scriptFiles, dbVersion)
    {
    }

    protected override string SqlCreateVersionsTable
        => """CREATE TABLE IF NOT EXISTS "scripts"."Versions" ("Version" TEXT PRIMARY KEY NOT NULL, "CreateOn" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);""";

    protected override string SqlVersionExists
        => """SELECT EXISTS (SELECT 1 FROM "scripts"."Versions" WHERE "Version" = @DbVersion);""";

    protected override string SqlInsertVersion
        => """INSERT INTO "scripts"."Versions" ("Version") VALUES (@DbVersion);""";

    protected override string SqlCreateTrackedTablesTable
        => """CREATE TABLE IF NOT EXISTS "scripts"."TrackedTables" ("TableName" TEXT NOT NULL);""";

    protected override string SqlReadTrackedTables
        => """SELECT "TableName" FROM "scripts"."TrackedTables";""";

    protected override string SqlTruncateTrackedTables
        => """DELETE FROM "scripts"."TrackedTables";""";

    protected override string SqlInsertTrackedTable
        => """INSERT INTO "scripts"."TrackedTables" ("TableName") VALUES (@TableName);""";

    public override IDbConnection CreateDbConnection()
        => GetOrCreateConnection(CreateConfiguredDbConnection);

    private IDbConnection CreateConfiguredDbConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder(ConnectionString);
        var attachedDatabases = GetAttachedDatabases(connectionStringBuilder.DataSource);
        if (connectionStringBuilder.Mode != SqliteOpenMode.Memory)
            return CreateConfiguredConnection(attachedDatabases);

        lock (_keepAliveLock)
        {
            if (_keepAliveConnection?.State != ConnectionState.Open)
            {
                _keepAliveConnection?.Dispose();
                _keepAliveConnection = new SqliteConnection(ConnectionString);
                _keepAliveConnection.Open();
                AttachDatabases(_keepAliveConnection, attachedDatabases);
            }
        }

        return CreateConfiguredConnection(attachedDatabases);
    }

    public void Dispose()
    {
        lock (_keepAliveLock)
        {
            _keepAliveConnection?.Dispose();
            _keepAliveConnection = null;
        }
    }

    private SqliteConnection CreateConfiguredConnection(
        IReadOnlyCollection<(string Schema, string DataSource)> attachedDatabases)
    {
        var connection = new SqliteConnection(ConnectionString);
        if (attachedDatabases.Count > 0)
        {
            connection.Open();
            AttachDatabases(connection, attachedDatabases);
        }
        return connection;
    }

    private IReadOnlyCollection<(string Schema, string DataSource)> GetAttachedDatabases(string dataSource)
        => InitializationScripts
            .Where(File.Exists)
            .SelectMany(fileName => MemoryDatabaseAttachment.Matches(File.ReadAllText(fileName)).Cast<Match>())
            .Select(match =>
            {
                var schema = match.Groups["schema"].Value;
                if (schema.StartsWith('"'))
                    schema = schema[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{dataSource}|{schema}")));
                return (Schema: schema, DataSource: $"file:DynamicSql-{key}?mode=memory&cache=shared");
            })
            .Append((Schema: "scripts", DataSource: GetScriptsDataSource(dataSource)))
            .DistinctBy(database => database.Schema, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string GetScriptsDataSource(string dataSource)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{dataSource}|scripts")));
        return dataSource.StartsWith("DynamicSql-", StringComparison.Ordinal)
            ? $"file:DynamicSql-{key}?mode=memory&cache=shared"
            : $"{dataSource}.scripts";
    }

    private static void AttachDatabases(
        SqliteConnection connection,
        IEnumerable<(string Schema, string DataSource)> attachedDatabases)
    {
        foreach (var (schema, dataSource) in attachedDatabases)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"ATTACH DATABASE '{dataSource}' AS {QuoteIdentifier(schema)};";
            command.ExecuteNonQuery();
        }
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string NormalizeConnectionString(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:")
        {
            if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
                builder.DataSource = $"DynamicSql-{Guid.NewGuid():N}";
            builder.Mode = SqliteOpenMode.Memory;
            builder.Cache = SqliteCacheMode.Shared;
        }
        return builder.ToString();
    }
}
