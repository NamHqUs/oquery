using System.Data;
using Oracle.ManagedDataAccess.Client;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public sealed class OracleDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance( connectionString, scriptFiles, dbVersion)
{
    protected override void EnsureDatabaseExists()
    {
    }

    protected override bool IsAlreadyExistsException(Exception exception)
        => exception is OracleException oracleException && oracleException.Number == 955;

    protected override string SqlCreateVersionsTable
        => """
BEGIN
    EXECUTE IMMEDIATE 'CREATE TABLE "OQUERY_TEST_VERSIONS" ("Version" VARCHAR2(255) NOT NULL PRIMARY KEY, "CreateOn" TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF;
END;
""";

    protected override string SqlVersionExists
        => """SELECT CASE WHEN EXISTS (SELECT 1 FROM "OQUERY_TEST_VERSIONS" WHERE "Version" = :DbVersion) THEN 1 ELSE 0 END FROM DUAL""";

    protected override string SqlInsertVersion
        => """INSERT INTO "OQUERY_TEST_VERSIONS" ("Version") VALUES (:DbVersion)""";

    protected override string SqlVersionParameterName => ":DbVersion";
    protected override string SqlTableNameParameterName => ":TableName";

    protected override string SqlCreateTrackedTablesTable
        => """CREATE TABLE "OQUERY_TEST_TRACKED_TABLES" ("TableName" VARCHAR2(1024) NOT NULL)""";

    protected override string SqlReadTrackedTables
        => """SELECT "TableName" FROM "OQUERY_TEST_TRACKED_TABLES";""";

    protected override string SqlTruncateTrackedTables
        => """TRUNCATE TABLE "OQUERY_TEST_TRACKED_TABLES";""";

    protected override string SqlInsertTrackedTable
        => """INSERT INTO "OQUERY_TEST_TRACKED_TABLES" ("TableName") VALUES (:TableName)""";

    public override IDbConnection CreateDbConnection()
        => GetOrCreateConnection(() => new OracleConnection(ConnectionString));
}
