using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using FirebirdSql.Data.FirebirdClient;
using Oracle.ManagedDataAccess.Client;
using MySqlConnector;
using Sap.Data.Hana;
using IBM.Data.Db2;
using System.Data;
using System.Text;

using OQuery;

namespace OQuery.UnitTest.Setup;

public enum DbServerType
{
    Sqlite,
    SqlServer,
    Postgres,
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
    {
        var cn = new NpgsqlConnection(
            "Host=localhost;Port=5432;Database=dynamicsqltest;Username=postgres;Password=NoPassword123!");
        cn.Open();

        lock (oLock)
        {
            var isExisted = ExecuteScalar<bool>(
                cn,
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = 'main' AND table_name = 'Users');");
            if (!isExisted)
                ExecuteNonQuery(cn, LoadScript("northstar-demo.postgres.sql"));
        }

        return cn;
    }

    public static IDbConnection GetNorthStarCockroachConnection()
    {
        var cn = new NpgsqlConnection(
            "Host=localhost;Port=26257;Database=defaultdb;Username=root;Ssl Mode=Disable;");
        cn.Open();
        ExecuteNonQuery(cn, "SET search_path = main, hr, sa, he, \"in\", public;");

        lock (oLock)
        {
            var isExisted = ExecuteScalar<bool>(
                cn,
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = 'main' AND table_name = 'Users');");
            if (!isExisted)
                ExecuteNonQuery(cn, LoadScript("northstar-demo.cockroach.sql"));
        }

        return cn;
    }

    public static IDbConnection GetNorthStarSqliteDockerConnection()
    {
        var root = new DirectoryInfo("../../../../../").FullName;
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
    {
        var connection = new FbConnection(
            "User=SYSDBA;Password=NoPassword123!;Database=localhost:/firebird/data/northstar.fdb");
        connection.Open();
        return connection;
    }

    public static IDbConnection GetNorthStarOracleConnection()
    {
        var connection = new OracleConnection(
            "User Id=NORTHSTAR;Password=NoPassword123!;Data Source=localhost:1521/FREEPDB1");
        connection.Open();
        return connection;
    }

    public static IDbConnection GetNorthStarMariaConnection()
    {
        var connection = new MySqlConnection(
            "Server=localhost;Port=3306;Database=northstar;User ID=root;Password=NoPassword123!;");
        connection.Open();
        return connection;
    }

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
    {
        var connection = new DB2Connection(
            "Server=localhost:50000;Database=NORTHSTA;UID=db2inst1;PWD=NoPassword123!;");
        connection.Open();
        return connection;
    }

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
    {
        var cn = new SqlConnection("Server=localhost;Database=DynamicSqlTest;User Id=sa;Password=NoPassword123!;TrustServerCertificate=True;");
        cn.Open();

        var isExisted = ExecuteScalar<int>(cn, "SELECT IIF(OBJECT_ID('dbo.[User]', 'U') IS NOT NULL, 1, 0);");
        if (isExisted != 1)
            ExecuteNonQuery(cn, LoadScript("oquery-test.sqlserver.sql", "sale-demo.sqlserver.sql", "northstar-demo.sqlserver.sql"));
        return cn;
    }

    private static IDbConnection CreatePostgresDbConnection()
    {
        var cn = new NpgsqlConnection(
            "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=NoPassword123!");
        cn.Open();

        lock (oLock)
        {
            var isExisted = ExecuteScalar<bool>(cn,
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = 'public' AND table_name = 'User');");

            if (!isExisted)
                ExecuteNonQuery(cn, LoadScript("oquery-test.postgres.sql", "sale-demo.postgres.sql"));
        }

        return cn;
    }

    private static IDbConnection CreateDb2DbConnection()
        => GetNorthStarDb2Connection();

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
