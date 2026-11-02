using System.Data;
using FirebirdSql.Data.FirebirdClient;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public sealed class FirebirdDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance( connectionString, scriptFiles, dbVersion)
{
    protected override void EnsureDatabaseExists()
    {
    }

    protected override string SqlCreateVersionsTable
        => """
EXECUTE BLOCK AS
BEGIN
    IF (NOT EXISTS (SELECT 1 FROM RDB$RELATIONS WHERE TRIM(RDB$RELATION_NAME) = 'Versions')) THEN
        EXECUTE STATEMENT 'CREATE TABLE "Versions" ("Version" VARCHAR(255) NOT NULL PRIMARY KEY, "CreateOn" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL)';
END
""";

    protected override string SqlVersionExists
        => """SELECT CASE WHEN EXISTS (SELECT 1 FROM "Versions" WHERE "Version" = @DbVersion) THEN 1 ELSE 0 END FROM RDB$DATABASE""";

    protected override string SqlInsertVersion
        => """INSERT INTO "Versions" ("Version") VALUES (@DbVersion)""";

    protected override string SqlCreateTrackedTablesTable
        => """
EXECUTE BLOCK AS
BEGIN
    IF (NOT EXISTS (SELECT 1 FROM RDB$RELATIONS WHERE TRIM(RDB$RELATION_NAME) = 'TrackedTables')) THEN
        EXECUTE STATEMENT 'CREATE TABLE "TrackedTables" ("TableName" VARCHAR(1024) NOT NULL)';
END
""";

    protected override string SqlReadTrackedTables
        => """SELECT "TableName" FROM "TrackedTables" """;

    protected override string SqlTruncateTrackedTables
        => """DELETE FROM "TrackedTables" """;

    protected override string SqlInsertTrackedTable
        => """INSERT INTO "TrackedTables" ("TableName") VALUES (@TableName)""";

    public override IDbConnection CreateDbConnection()
        => GetOrCreateConnection(() => new FbConnection(ConnectionString));
}
