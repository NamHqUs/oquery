using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OQuery.Database;

public sealed class SqliteDbInstance : DbInstance, IDisposable
{
    private const string CrossSchemaRelationsTable = "__DynamicSqlRelations";

    private static readonly Regex MemoryDatabaseAttachment = new(
        """ATTACH\s+DATABASE\s+':memory:'\s+AS\s+(?<schema>"(?:""|[^"])+"|[A-Za-z_][A-Za-z0-9_]*)\s*;""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly object _keepAliveLock = new();

    private bool? _inMemory;
    private bool InMemory => _inMemory ??= ConnectionString.Contains(":memory:");
    private SqliteConnection? _keepAliveConnection;

    public SqliteDbInstance(string connectionString, string[] scriptFiles, string dbVersion)
        : base("Sqlite", NormalizeConnectionString(connectionString), scriptFiles, dbVersion)
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

    protected override string SqlGetColumns => "SELECT NULL AS SchemaName, NULL AS TableName, NULL AS ColumnName, NULL AS DataType, 0 AS IsNullable, 0 AS IsPrimaryKey WHERE 1 = 0;";
    protected override string SqlGetRelationships => "SELECT NULL AS RelationName, NULL AS SourceSchema, NULL AS SourceTable, NULL AS SourceColumn, NULL AS TargetSchema, NULL AS TargetTable, NULL AS TargetColumn, 0 AS Ordinal, 0 AS IsUnique WHERE 1 = 0;";

    protected override string PrepareInitializationScript(string script)
        => MemoryDatabaseAttachment.Replace(script, string.Empty);

    public override bool TestConnection()
    {
        if (!InMemory)
            return base.TestConnection();

        try
        {
            var cn = CreateDbConnection();
            if (cn.State != ConnectionState.Open)
                cn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
    public override DatabaseSchemaDto GetDatabaseSchema()
    {
        using var connection = CreateDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        var dbConnection = connection as DbConnection
            ?? throw new InvalidOperationException("The configured database connection does not support schema discovery.");

        var tables = new List<DatabaseTableDto>();
        var relationships = new List<DatabaseRelationshipDto>();
        foreach (var table in GetTables(dbConnection))
        {
            var columns = ReadSchemaRows(dbConnection,
                $"PRAGMA {QuoteIdentifier(table.Schema)}.table_info({QuoteLiteral(table.Name)});");
            var tableColumns = columns.Rows.Cast<DataRow>()
                .Select(row => new DatabaseColumnDto(
                    row["name"]?.ToString() ?? string.Empty,
                    row["type"]?.ToString() ?? string.Empty,
                    Convert.ToInt32(row["pk"]) > 0,
                    Convert.ToInt32(row["notnull"]) == 0 && Convert.ToInt32(row["pk"]) == 0))
                .ToArray();
            tables.Add(new DatabaseTableDto(table.Schema, table.Name, tableColumns));

            var uniqueColumnSets = GetUniqueColumnSets(dbConnection, table.Schema, table.Name);
            var foreignKeys = ReadSchemaRows(dbConnection,
                $"PRAGMA {QuoteIdentifier(table.Schema)}.foreign_key_list({QuoteLiteral(table.Name)});");
            foreach (var foreignKey in foreignKeys.Rows.Cast<DataRow>().GroupBy(row => Convert.ToInt32(row["id"])))
            {
                var orderedColumns = foreignKey.OrderBy(row => Convert.ToInt32(row["seq"])).ToArray();
                var sourceColumns = orderedColumns.Select(row => row["from"]?.ToString() ?? string.Empty).ToArray();
                var targetColumns = orderedColumns.Select(row => row["to"]?.ToString() ?? string.Empty).ToArray();
                relationships.Add(new DatabaseRelationshipDto(
                    $"FK_{table.Schema}_{table.Name}_{foreignKey.Key}",
                    table.Schema,
                    table.Name,
                    sourceColumns,
                    table.Schema,
                    orderedColumns[0]["table"]?.ToString() ?? string.Empty,
                    targetColumns,
                    uniqueColumnSets.Any(uniqueColumns => uniqueColumns.SetEquals(sourceColumns))));
            }
        }

        relationships.AddRange(ReadCrossSchemaRelationships(dbConnection));
        return new DatabaseSchemaDto(tables, relationships);
    }

    public override IDbConnection CreateDbConnection()
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

    private IReadOnlyList<(string Schema, string Name)> GetTables(DbConnection connection)
    {
        var schemas = ReadSchemaRows(connection, "PRAGMA database_list;").Rows.Cast<DataRow>()
            .Select(row => row["name"]?.ToString() ?? string.Empty)
            .Where(schema => !string.IsNullOrWhiteSpace(schema) &&
                !string.Equals(schema, "temp", StringComparison.OrdinalIgnoreCase));
        var tables = new List<(string Schema, string Name)>();
        foreach (var schema in schemas)
        {
            var schemaTables = ReadSchemaRows(connection,
                $"SELECT name FROM {QuoteIdentifier(schema)}.sqlite_master " +
                "WHERE type = 'table' AND name NOT GLOB 'sqlite_*' ORDER BY name;");
            tables.AddRange(schemaTables.Rows.Cast<DataRow>()
                .Select(row => (Schema: schema, Name: row["name"]?.ToString() ?? string.Empty))
                .Where(table => !string.Equals(table.Schema, "main", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(table.Name, CrossSchemaRelationsTable, StringComparison.OrdinalIgnoreCase)));
        }

        return tables;
    }

    private List<DatabaseRelationshipDto> ReadCrossSchemaRelationships(DbConnection connection)
    {
        var metadataTable = ReadSchemaRows(connection,
            $"SELECT 1 FROM {QuoteIdentifier("main")}.sqlite_master " +
            $"WHERE type = 'table' AND name = {QuoteLiteral(CrossSchemaRelationsTable)};");
        if (metadataTable.Rows.Count == 0)
            return [];

        var rows = ReadSchemaRows(connection,
            $"SELECT RelationName, SourceSchema, SourceTable, SourceColumn, " +
            $"TargetSchema, TargetTable, TargetColumn, Ordinal, IsUnique " +
            $"FROM {QuoteIdentifier("main")}.{QuoteIdentifier(CrossSchemaRelationsTable)} " +
            "ORDER BY RelationName, Ordinal;");
        return rows.Rows.Cast<DataRow>()
            .GroupBy(row => row["RelationName"]?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                var orderedRows = group.OrderBy(row => Convert.ToInt32(row["Ordinal"])).ToArray();
                var first = orderedRows[0];
                return new DatabaseRelationshipDto(
                    first["RelationName"]?.ToString() ?? string.Empty,
                    first["SourceSchema"]?.ToString() ?? string.Empty,
                    first["SourceTable"]?.ToString() ?? string.Empty,
                    orderedRows.Select(row => row["SourceColumn"]?.ToString() ?? string.Empty).ToArray(),
                    first["TargetSchema"]?.ToString() ?? string.Empty,
                    first["TargetTable"]?.ToString() ?? string.Empty,
                    orderedRows.Select(row => row["TargetColumn"]?.ToString() ?? string.Empty).ToArray(),
                    Convert.ToBoolean(first["IsUnique"]));
            })
            .ToList();
    }

    private HashSet<string>[] GetUniqueColumnSets(DbConnection connection, string schema, string tableName)
    {
        var indexes = ReadSchemaRows(connection,
            $"PRAGMA {QuoteIdentifier(schema)}.index_list({QuoteLiteral(tableName)});");
        return indexes.Rows.Cast<DataRow>()
            .Where(row => Convert.ToInt32(row["unique"]) == 1)
            .Select(row =>
            {
                var indexName = row["name"]?.ToString() ?? string.Empty;
                var columns = ReadSchemaRows(connection,
                    $"PRAGMA {QuoteIdentifier(schema)}.index_info({QuoteLiteral(indexName)});");
                return columns.Rows.Cast<DataRow>()
                    .Select(column => column["name"]?.ToString() ?? string.Empty)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            })
            .ToArray();
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

    private static string QuoteLiteral(string value)
        => $"'{value.Replace("'", "''")}'";

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
