using IBM.Data.Db2;
using System.Data;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public sealed class Db2DbInstance(string connectionString, string[] scriptFiles, string dbVersion)
    : DbInstance(connectionString, scriptFiles, dbVersion)
{
    protected override void EnsureDatabaseExists()
    {
    }

    protected override bool IsAlreadyExistsException(Exception exception)
        => exception is DB2Exception db2Exception
            && db2Exception.Message.Contains("SQL0601N", StringComparison.Ordinal);

    protected override string SqlCreateVersionsTable
        => "CREATE TABLE scripts.Versions (Version varchar(255) NOT NULL PRIMARY KEY, CreateOn timestamp NOT NULL DEFAULT CURRENT TIMESTAMP);";

    protected override string SqlVersionExists
        => "SELECT CASE WHEN EXISTS (SELECT 1 FROM scripts.Versions WHERE Version = @DbVersion) THEN 1 ELSE 0 END FROM SYSIBM.SYSDUMMY1";

    protected override string SqlInsertVersion
        => "INSERT INTO scripts.Versions (Version) VALUES (@DbVersion)";

    protected override string SqlCreateTrackedTablesTable
        => "CREATE TABLE scripts.TrackedTables (TableName varchar(1024) NOT NULL)";

    protected override string SqlReadTrackedTables
        => "SELECT TableName FROM scripts.TrackedTables";

    protected override string SqlTruncateTrackedTables
        => "TRUNCATE TABLE scripts.TrackedTables IMMEDIATE";

    protected override string SqlInsertTrackedTable
        => "INSERT INTO scripts.TrackedTables (TableName) VALUES (@TableName)";

    public override IDbConnection CreateDbConnection()
        => GetOrCreateConnection(() => new DB2Connection(ConnectionString));
}
