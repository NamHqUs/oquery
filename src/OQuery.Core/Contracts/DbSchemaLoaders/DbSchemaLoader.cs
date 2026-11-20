using System.Data;
using System.Data.Common;
using System.Globalization;
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace OQuery;

/// <summary>
/// Reads schema and table data from a provider database. Provisioning (version table, seed scripts) is owned by the host.
/// </summary>
public abstract class DbSchemaLoader
{
    private readonly string _identifierQuotePrefix;
    private readonly string _identifierQuoteSuffix;

    protected abstract string SqlGetColumns { get; }
    protected abstract string SqlGetRelationships { get; }

    protected DbSchemaLoader(
        string provider,
        string connectionString,
        string[] initializationScripts,
        string identifierQuotePrefix,
        string identifierQuoteSuffix)
    {
        Provider = provider;
        ConnectionString = connectionString;
        InitializationScripts = initializationScripts;
        _identifierQuotePrefix = identifierQuotePrefix;
        _identifierQuoteSuffix = identifierQuoteSuffix;
    }

    public string Provider { get; }

    protected string ConnectionString { get; }

    protected string[] InitializationScripts { get; }

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

    protected string QuoteIdentifier(string identifier)
        => $"{_identifierQuotePrefix}{identifier.Replace(_identifierQuoteSuffix, _identifierQuoteSuffix + _identifierQuoteSuffix, StringComparison.Ordinal)}{_identifierQuoteSuffix}";

    protected static T ExecuteRawSql<T>(IDbConnection connection, string sql, Func<IDbCommand, T> execute)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (connection.State != ConnectionState.Open)
            connection.Open();
        return execute(command);
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
}
