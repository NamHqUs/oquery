using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OQuery.Database;

public abstract class DbInstance
{
    private static HashSet<string> _initializeDbs = [];
    private static object _initializationLock = new();

    public readonly string Provider;
    protected readonly string ConnectionString;
    protected readonly string[] InitializationScripts;
    protected readonly string DbVersion;
    protected abstract string SqlCreateVersionsTable { get; }
    protected abstract string SqlVersionExists { get; }
    protected abstract string SqlInsertVersion { get; }
    protected abstract string SqlCreateTrackedTablesTable { get; }
    protected abstract string SqlReadTrackedTables { get; }
    protected abstract string SqlTruncateTrackedTables { get; }
    protected abstract string SqlInsertTrackedTable { get; }
    protected virtual string SqlVersionParameterName => "@DbVersion";
    protected virtual string SqlTableNameParameterName => "@TableName";
    protected abstract string SqlGetColumns { get; }
    protected abstract string SqlGetRelationships { get; }
    protected virtual string IdentifierQuotePrefix => "\"";
    protected virtual string IdentifierQuoteSuffix => "\"";

    protected DbInstance(string provider, string connectionString, string[] scriptFields, string dbVersion)
    {
        Provider = provider;
        ConnectionString = connectionString;
        InitializationScripts = scriptFields;
        DbVersion = string.IsNullOrWhiteSpace(dbVersion)
            ? throw new ArgumentException("A database version is required.", nameof(dbVersion))
            : dbVersion;
        lock (_initializationLock)
        {
            TestConnection();
            if (_initializeDbs.Add(connectionString))
                EnsureDatabaseExists(scriptFields);
        }
    }

    public abstract IDbConnection CreateDbConnection();


    public virtual bool TestConnection()
    {
        try
        {
            using var connection = CreateDbConnection();
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public virtual DatabaseSchemaDto GetDatabaseSchema()
    {
        using var connection = CreateDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var dbConnection = connection as DbConnection
            ?? throw new InvalidOperationException("The configured database connection does not support schema discovery.");

        var columns = ReadSchemaRows(dbConnection, SqlGetColumns);
        var relationships = ReadSchemaRows(dbConnection, SqlGetRelationships);
        var tables = columns.Rows
            .Cast<DataRow>()
            .GroupBy(row => (Schema: Value(row, "SchemaName"), Name: Value(row, "TableName")))
            .Select(group => new DatabaseTableDto(
                group.Key.Schema,
                group.Key.Name,
                group.Select(row => new DatabaseColumnDto(
                    Value(row, "ColumnName"),
                    Value(row, "DataType"),
                    Convert.ToBoolean(row["IsPrimaryKey"]),
                    Convert.ToBoolean(row["IsNullable"])))
                    .ToArray()))
            .ToArray();

        var relationDtos = relationships.Rows
            .Cast<DataRow>()
            .GroupBy(row => Value(row, "RelationName"), StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new DatabaseRelationshipDto(
                    Value(first, "RelationName"),
                    Value(first, "SourceSchema"),
                    Value(first, "SourceTable"),
                    group.OrderBy(row => Convert.ToInt32(row["Ordinal"]))
                        .Select(row => Value(row, "SourceColumn"))
                        .ToArray(),
                    Value(first, "TargetSchema"),
                    Value(first, "TargetTable"),
                    group.OrderBy(row => Convert.ToInt32(row["Ordinal"]))
                        .Select(row => Value(row, "TargetColumn"))
                        .ToArray(),
                    Convert.ToBoolean(first["IsUnique"]));
            })
            .ToArray();

        return new DatabaseSchemaDto(tables, relationDtos);
    }

    public DatabaseTableDataDto GetTableData(string tableName, string? schema = null, int? limit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        if (limit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "The row limit must be greater than zero.");
        var rowLimit = Math.Min(limit ?? 10, 10);

        var matchingTables = GetDatabaseSchema().Tables.Where(table =>
            string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(schema)
                || string.Equals(table.Schema, schema, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (matchingTables.Length == 0)
            throw new KeyNotFoundException($"Table '{tableName}' was not found.");
        if (matchingTables.Length > 1)
            throw new ArgumentException($"Table name '{tableName}' is ambiguous. Specify a schema.", nameof(tableName));

        var selectedTable = matchingTables[0];
        using var connection = CreateDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        var qualifiedName = QuoteIdentifier(selectedTable.Name);
        if (!string.IsNullOrWhiteSpace(selectedTable.Schema))
            qualifiedName = $"{QuoteIdentifier(selectedTable.Schema)}.{qualifiedName}";
        command.CommandText = $"SELECT * FROM {qualifiedName}";
        using var reader = command.ExecuteReader();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = new List<IReadOnlyList<string>>();
        while (rows.Count < rowLimit && reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = reader.IsDBNull(index)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
            rows.Add(row);
        }

        return new DatabaseTableDataDto(selectedTable.Name, columns, rows);
    }

    protected virtual void EnsureDatabaseExists(string[] scriptFiles)
    {
        using var cn = CreateDbConnection();
        ExecuteNonQuery(cn, SqlCreateVersionsTable);
        ExecuteNonQuery(cn, SqlCreateTrackedTablesTable);
        if (VersionExists(cn))
            return;
        if (scriptFiles.Length == 0)
            return;

        DropTrackedTables(cn);
        ExecuteNonQuery(cn, SqlTruncateTrackedTables);
        foreach (var fileName in scriptFiles)
        {
            var filePath = Path.Combine(Environment.CurrentDirectory, fileName);
            var scripts = File.ReadAllText(filePath);
            ExecuteNonQuery(cn, PrepareInitializationScript(scripts));
            InsertTrackedTables(cn, scripts);
        }

        InsertVersion(cn);
    }

    protected virtual string PrepareInitializationScript(string script) => script;

    protected virtual DataTable ReadSchemaRows(DbConnection connection, string sql)
    {
        return ExecuteRawSql(connection, sql, command =>
        {
            using var reader = command.ExecuteReader();
            var rows = new DataTable();
            for (var index = 0; index < reader.FieldCount; index++)
                rows.Columns.Add(reader.GetName(index), typeof(object));

            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Rows.Add(values);
            }

            return rows;
        });
    }

    private static string Value(DataRow row, string columnName)
        => row[columnName]?.ToString() ?? string.Empty;

    private string QuoteIdentifier(string identifier)
        => $"{IdentifierQuotePrefix}{identifier.Replace(IdentifierQuoteSuffix, IdentifierQuoteSuffix + IdentifierQuoteSuffix, StringComparison.Ordinal)}{IdentifierQuoteSuffix}";

    private static T ExecuteScalar<T>(IDbConnection cn, string sql)
        => ExecuteRawSql(cn, sql,
            command => (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T)));

    private bool VersionExists(IDbConnection cn)
        => ExecuteRawSql(cn, SqlVersionExists, command =>
        {
            AddVersionParameter(command);
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        });

    private void InsertVersion(IDbConnection cn)
        => ExecuteRawSql(cn, SqlInsertVersion, command =>
        {
            AddVersionParameter(command);
            command.ExecuteNonQuery();
        });

    private void AddVersionParameter(IDbCommand command)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = SqlVersionParameterName;
        parameter.DbType = DbType.String;
        parameter.Value = DbVersion;
        command.Parameters.Add(parameter);
    }

    private void DropTrackedTables(IDbConnection cn)
    {
        var tableNames = ExecuteRawSql(cn, SqlReadTrackedTables, command =>
        {
            using var reader = command.ExecuteReader();
            var names = new List<string>();
            while (reader.Read())
                names.Add(reader.GetString(0));
            return names;
        });

        foreach (var tableName in tableNames.Distinct(StringComparer.OrdinalIgnoreCase).Reverse())
            ExecuteNonQuery(cn, BuildDropTableSql(tableName));
    }

    private void InsertTrackedTables(IDbConnection cn, string script)
    {
        var tableNames = Regex.Matches(
                script,
                @"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:(?:""[^""]+""|\[[^\]]+\]|`[^`]+`|[A-Za-z_][\w$]*)\s*\.\s*)?(?:""[^""]+""|\[[^\]]+\]|`[^`]+`|[A-Za-z_][\w$]*))",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var tableName in tableNames)
            ExecuteNonQuery(cn, SqlInsertTrackedTable, SqlTableNameParameterName, tableName);
    }

    protected virtual string BuildDropTableSql(string tableName)
    {
        var parts = tableName.Split('.', StringSplitOptions.TrimEntries);
        var qualifiedName = string.Join(".", parts.Select(UnquoteIdentifier).Select(QuoteIdentifier));
        return $"DROP TABLE IF EXISTS {qualifiedName};";
    }

    private static string UnquoteIdentifier(string identifier)
    {
        if (identifier.Length >= 2
            && ((identifier[0] == '"' && identifier[^1] == '"')
                || (identifier[0] == '[' && identifier[^1] == ']')
                || (identifier[0] == '`' && identifier[^1] == '`')))
            return identifier[1..^1];
        return identifier;
    }

    private static void ExecuteNonQuery(IDbConnection cn, string sql)
        => ExecuteRawSql(cn, sql, command => command.ExecuteNonQuery());

    private static void ExecuteNonQuery(
        IDbConnection cn,
        string sql,
        string parameterName,
        string parameterValue)
        => ExecuteRawSql(cn, sql, command =>
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.DbType = DbType.String;
            parameter.Value = parameterValue;
            command.Parameters.Add(parameter);
            command.ExecuteNonQuery();
        });

    private static T ExecuteRawSql<T>(
        IDbConnection connection,
        string sql,
        Func<IDbCommand, T> execute)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (connection.State != ConnectionState.Open)
            connection.Open();
        return execute(command);
    }

    private static void ExecuteRawSql(
        IDbConnection connection,
        string sql,
        Action<IDbCommand> execute)
        => ExecuteRawSql<object?>(connection, sql, command =>
        {
            execute(command);
            return null;
        });

}
