using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using FirebirdSql.Data.FirebirdClient;
using Oracle.ManagedDataAccess.Client;
using MySqlConnector;
using Sap.Data.Hana;
using IBM.Data.Db2;
using System.Data;
using System.Security.Cryptography;
using System.Text;

using OQuery;
using OQuery.UnitTest.Setup.DbInstances;

namespace OQuery.UnitTest.Setup;

public enum DbServerType
{
    Sqlite,
    SqlServer,
    Firebird,
    Postgres,
    Cockroach,
    MySql,
    Oracle,
    Db2
}
internal sealed class TestDatabase
{
    private static readonly object oLock = new();

    public static IDbConnection GetDbConnection(DbServerType dbServerType = DbServerType.Sqlite)
        => dbServerType switch
        {
            DbServerType.Sqlite => CreateSqliteDbConnection(),
            DbServerType.SqlServer => CreateSqlServerDbConnection(),
            DbServerType.Postgres => CreatePostgresDbConnection(),
            DbServerType.Db2 => CreateDb2DbConnection(),
            _ => throw new ArgumentOutOfRangeException(nameof(dbServerType), dbServerType, null)
        };

    public static IDbConnection GetNorthStarSqliteConnection()
    {
        var cn = new SqliteConnection("Data Source=:memory:");
        cn.Open();

        var isExisted = ExecuteScalar<long>(
            cn,
            "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'dbo.User')");
        if (isExisted != 1)
        {
            var script = new StringBuilder("ATTACH DATABASE ':memory:' AS dbo;");
            script.Append(LoadScript("oquery-test.sqlite.sql", "northstar-demo.sqlite.sql"));
            ExecuteNonQuery(cn, script.ToString());
        }

        return cn;
    }

    public static IDbConnection GetNorthStarPostgresConnection()
        => CreatePostgresInstance(
            "Host=localhost;Port=5432;Database=dynamicsqltest;Username=postgres;Password=NoPassword123!",
            "northstar-demo.postgres.sql");

    public static IDbConnection GetNorthStarCockroachConnection()
        => CreateCockroachInstance(
            "Host=localhost;Port=26257;Database=defaultdb;Username=root;Ssl Mode=Disable;",
            "northstar-demo.cockroach.sql");

    public static IDbConnection GetNorthStarSqliteDockerConnection()
    {
        var root = new DirectoryInfo(
            Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
        var cn = new SqliteConnection(
            $"Data Source={Path.Combine(root, "docker", "sqlite-data", "main.db")}");
        cn.Open();

        var dataPath = Path.Combine(root, "docker", "sqlite-data").Replace("\\", "/", StringComparison.Ordinal);
        using var command = cn.CreateCommand();
        command.CommandText =
            $"ATTACH DATABASE '{dataPath}/hr.db' AS hr;" +
            $"ATTACH DATABASE '{dataPath}/sa.db' AS sa;" +
            $"ATTACH DATABASE '{dataPath}/he.db' AS he;" +
            $"ATTACH DATABASE '{dataPath}/in.db' AS \"in\";";
        command.ExecuteNonQuery();
        return cn;
    }

    public static IDbConnection GetNorthStarFirebirdConnection()
        => CreateFirebirdInstance(
            "User=SYSDBA;Password=NoPassword123!;Database=localhost:/firebird/data/northstar.fdb",
            "northstar-demo.firebird.sql");

    public static IDbConnection GetNorthStarOracleConnection()
        => CreateOracleInstance(
            "User Id=NORTHSTAR;Password=NoPassword123!;Data Source=localhost:1521/FREEPDB1",
            "northstar-demo.oracle.sql");

    public static IDbConnection GetNorthStarMariaConnection()
        => CreateMySqlInstance(
            "Server=localhost;Port=3306;Database=northstar;User ID=root;Password=NoPassword123!;",
            "northstar-demo.mariadb.sql");

    public static IDbConnection GetNorthStarSapHanaConnection()
    {
        var connection = new HanaConnection(
            "Server=localhost:39017;UserID=SYSTEM;Password=NoPassword123!;");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SET SCHEMA \"NORTHSTAR\"";
        command.ExecuteNonQuery();
        return connection;
    }

    public static IDbConnection GetNorthStarDb2Connection()
        => CreateDb2Instance(
            "Server=localhost:50000;Database=NORTHSTA;UID=db2inst1;PWD=NoPassword123!;",
            "northstar-demo.db2.sql");

    public static IDbConnection GetNorthStarSqlServerConnection()
        => CreateSqlServerDbConnection();

    private static IDbConnection CreateSqliteDbConnection()
    {
        var cn = new SqliteConnection("Data Source=:memory:");
        cn.Open();

        var isExisted = ExecuteScalar<long>(cn, 
            "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'dbo.User')");

        if (isExisted != 1)
        {
            StringBuilder sb = new("ATTACH DATABASE ':memory:' AS dbo;");
            sb.Append(LoadScript("oquery-test.sqlite.sql", "sale-demo.sqlite.sql"));
            ExecuteNonQuery(cn, sb.ToString());
        }

        return cn;
    }

    private static IDbConnection CreateSqlServerDbConnection()
        => CreateSqlServerInstance(
            "Server=localhost;Database=DynamicSqlTest;User Id=sa;Password=NoPassword123!;TrustServerCertificate=True;",
            "oquery-test.sqlserver.sql",
            "sale-demo.sqlserver.sql",
            "northstar-demo.sqlserver.sql");

    private static IDbConnection CreatePostgresDbConnection()
        => CreatePostgresInstance(
            "Host=localhost;Port=5432;Database=DynamicSqlTest;Username=postgres;Password=NoPassword123!",
            "oquery-test.postgres.sql",
            "sale-demo.postgres.sql");

    private static IDbConnection CreateDb2DbConnection()
        => GetNorthStarDb2Connection();

    private static IDbConnection CreateSqlServerInstance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new SqlServerDbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection CreatePostgresInstance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new PostgresDbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection CreateCockroachInstance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new CockroachDbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection CreateFirebirdInstance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new FirebirdDbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection CreateOracleInstance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new OracleDbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection CreateMySqlInstance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new MySqlDbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection CreateDb2Instance(
        string connectionString,
        params string[] scriptNames)
        => OpenConnection(new Db2DbInstance(
            connectionString,
            GetScriptPaths(scriptNames),
            GetScriptVersion(scriptNames)));

    private static IDbConnection OpenConnection(DbInstance instance)
    {
        var connection = instance.CreateDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        return connection;
    }

    private static string[] GetScriptPaths(IEnumerable<string> scriptNames)
        => scriptNames
            .Select(name => Path.Combine("data-storage", "scripts", name))
            .ToArray();

    private static string GetScriptVersion(IEnumerable<string> scriptNames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var root = new DirectoryInfo("../../../../../").FullName;

        foreach (var scriptName in scriptNames)
        {
            var path = Path.Combine(root, "data-storage", "scripts", scriptName);
            hash.AppendData(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }


    private static string LoadScript(params string[] fileNames)
    {
        var path = new DirectoryInfo("../../../../../").FullName;
        return string.Join(Environment.NewLine, fileNames.Select(fileName => File.ReadAllText(Path.Combine(path, "data-storage", "scripts", fileName))));
    }
    private static void ExecuteNonQuery(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static T ExecuteScalar<T>(IDbConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }
}
