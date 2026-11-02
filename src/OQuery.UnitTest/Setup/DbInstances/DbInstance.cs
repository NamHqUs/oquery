using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public abstract class DbInstance
{
    private static readonly HashSet<string> InitializedDatabases = [];
    private static readonly object InitializationLock = new();
    private IDbConnection? _connection;

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
    protected virtual string IdentifierQuotePrefix => "\"";
    protected virtual string IdentifierQuoteSuffix => "\"";
    protected virtual bool IsAlreadyExistsException(Exception exception) => false;

    protected DbInstance(string connectionString, string[] scriptFields, string dbVersion)
    {
        ConnectionString = connectionString;
        InitializationScripts = scriptFields;
        DbVersion = string.IsNullOrWhiteSpace(dbVersion)
            ? throw new ArgumentException("A database version is required.", nameof(dbVersion))
            : dbVersion;
        lock (InitializationLock)
        {
            var databaseKey = $"{connectionString}\0{dbVersion}";
            if (TestConnection() && !InitializedDatabases.Contains(databaseKey))
            {
                EnsureDatabaseExists();
                InitializedDatabases.Add(databaseKey);
            }
        }
    }

    public abstract IDbConnection CreateDbConnection();

    protected IDbConnection GetOrCreateConnection(Func<IDbConnection> factory)
        => _connection ??= factory();

    public virtual bool TestConnection()
    {
        try
        {
            var connection = CreateDbConnection();
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected virtual void EnsureDatabaseExists()
    {
        var cn = CreateDbConnection();
        try
        {
            ExecuteNonQuery(cn, SqlCreateVersionsTable);
        }
        catch (Exception exception) when (IsAlreadyExistsException(exception))
        {
        }

        if (VersionExists(cn))
            return;

        try
        {
            ExecuteNonQuery(cn, SqlCreateTrackedTablesTable);
        }
        catch (Exception exception) when (IsAlreadyExistsException(exception))
        {
        }
        if (InitializationScripts.Length == 0)
            return;

        DropTrackedTables(cn);
        ExecuteNonQuery(cn, SqlTruncateTrackedTables);
        foreach (var fileName in InitializationScripts)
        {
            var filePath = ResolveScriptPath(fileName);
            var scripts = File.ReadAllText(filePath);
            ExecuteNonQuery(cn, PrepareInitializationScript(scripts));
            InsertTrackedTables(cn, scripts);
        }

        InsertVersion(cn);
    }

    protected virtual string PrepareInitializationScript(string script) => script;

    private static string ResolveScriptPath(string fileName)
    {
        if (Path.IsPathRooted(fileName))
            return fileName;

        var currentPath = Path.Combine(Environment.CurrentDirectory, fileName);
        if (File.Exists(currentPath))
            return currentPath;

        return Path.Combine(
            new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName,
            fileName);
    }

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

    private string QuoteIdentifier(string identifier)
        => $"{IdentifierQuotePrefix}{identifier.Replace(IdentifierQuoteSuffix, IdentifierQuoteSuffix + IdentifierQuoteSuffix, StringComparison.Ordinal)}{IdentifierQuoteSuffix}";

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
