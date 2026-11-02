using Npgsql;
using System.Data;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public class PostgresDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance( connectionString, scriptFiles, dbVersion)
{
    protected override string SqlCreateVersionsTable
        => """CREATE SCHEMA IF NOT EXISTS "scripts"; CREATE TABLE IF NOT EXISTS "scripts"."Versions" ("Version" varchar(255) PRIMARY KEY NOT NULL, "CreateOn" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);""";

    protected override string SqlVersionExists
        => """SELECT CASE WHEN EXISTS (SELECT 1 FROM "scripts"."Versions" WHERE "Version" = @DbVersion) THEN 1 ELSE 0 END;""";

    protected override string SqlInsertVersion
        => """INSERT INTO "scripts"."Versions" ("Version") VALUES (@DbVersion);""";

    protected override string SqlCreateTrackedTablesTable
        => """CREATE TABLE IF NOT EXISTS "scripts"."TrackedTables" ("TableName" varchar(1024) NOT NULL);""";

    protected override string SqlReadTrackedTables
        => """SELECT "TableName" FROM "scripts"."TrackedTables";""";

    protected override string SqlTruncateTrackedTables
        => """TRUNCATE TABLE "scripts"."TrackedTables";""";

    protected override string SqlInsertTrackedTable
        => """INSERT INTO "scripts"."TrackedTables" ("TableName") VALUES (@TableName);""";

    public override IDbConnection CreateDbConnection()
        => GetOrCreateConnection(() => new NpgsqlConnection(ConnectionString));
}
