using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace OQuery.Database;

public sealed class OracleDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance("Oracle", connectionString, scriptFiles, dbVersion)
{
    protected override string SqlCreateVersionsTable
        => """
BEGIN
    EXECUTE IMMEDIATE 'CREATE TABLE "scripts"."Versions" ("Version" VARCHAR2(255) NOT NULL PRIMARY KEY, "CreateOn" TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF;
END;
""";

    protected override string SqlVersionExists
        => """SELECT CASE WHEN EXISTS (SELECT 1 FROM "scripts"."Versions" WHERE "Version" = :DbVersion) THEN 1 ELSE 0 END FROM DUAL""";

    protected override string SqlInsertVersion
        => """INSERT INTO "scripts"."Versions" ("Version") VALUES (:DbVersion)""";

    protected override string SqlVersionParameterName => ":DbVersion";
    protected override string SqlTableNameParameterName => ":TableName";

    protected override string SqlCreateTrackedTablesTable
        => """CREATE TABLE "scripts"."TrackedTables" ("TableName" VARCHAR2(1024) NOT NULL)""";

    protected override string SqlReadTrackedTables
        => """SELECT "TableName" FROM "scripts"."TrackedTables";""";

    protected override string SqlTruncateTrackedTables
        => """TRUNCATE TABLE "scripts"."TrackedTables";""";

    protected override string SqlInsertTrackedTable
        => """INSERT INTO "scripts"."TrackedTables" ("TableName") VALUES (:TableName)""";

    protected override string SqlGetColumns => """
        SELECT
            OWNER AS "SchemaName",
            TABLE_NAME AS "TableName",
            COLUMN_NAME AS "ColumnName",
            DATA_TYPE AS "DataType",
            CASE WHEN NULLABLE = 'Y' THEN 1 ELSE 0 END AS "IsNullable",
            CASE WHEN COLUMN_NAME IN (
                SELECT primaryColumns.COLUMN_NAME
                FROM ALL_CONSTRAINTS constraints
                INNER JOIN ALL_CONS_COLUMNS primaryColumns
                    ON primaryColumns.OWNER = constraints.OWNER
                    AND primaryColumns.CONSTRAINT_NAME = constraints.CONSTRAINT_NAME
                WHERE constraints.CONSTRAINT_TYPE = 'P'
                    AND constraints.OWNER = ALL_TAB_COLUMNS.OWNER
                    AND constraints.TABLE_NAME = ALL_TAB_COLUMNS.TABLE_NAME
            ) THEN 1 ELSE 0 END AS "IsPrimaryKey"
        FROM ALL_TAB_COLUMNS
        ORDER BY OWNER, TABLE_NAME, COLUMN_ID
        """;

    protected override string SqlGetRelationships => """
        SELECT
            foreignKeys.CONSTRAINT_NAME AS "RelationName",
            foreignKeys.OWNER AS "SourceSchema",
            foreignKeys.TABLE_NAME AS "SourceTable",
            foreignColumns.COLUMN_NAME AS "SourceColumn",
            primaryKeys.OWNER AS "TargetSchema",
            primaryKeys.TABLE_NAME AS "TargetTable",
            primaryColumns.COLUMN_NAME AS "TargetColumn",
            foreignColumns.POSITION AS "Ordinal",
            CASE WHEN EXISTS (
                SELECT 1
                FROM ALL_CONSTRAINTS uniqueKeys
                WHERE uniqueKeys.OWNER = foreignKeys.OWNER
                    AND uniqueKeys.TABLE_NAME = foreignKeys.TABLE_NAME
                    AND uniqueKeys.CONSTRAINT_TYPE IN ('P', 'U')
                    AND (SELECT COUNT(*) FROM ALL_CONS_COLUMNS uniqueColumns
                         WHERE uniqueColumns.OWNER = uniqueKeys.OWNER
                            AND uniqueColumns.CONSTRAINT_NAME = uniqueKeys.CONSTRAINT_NAME)
                        = (SELECT COUNT(*) FROM ALL_CONS_COLUMNS fkColumns
                           WHERE fkColumns.OWNER = foreignKeys.OWNER
                            AND fkColumns.CONSTRAINT_NAME = foreignKeys.CONSTRAINT_NAME)
                    AND NOT EXISTS (
                        SELECT 1
                        FROM ALL_CONS_COLUMNS fkColumns
                        WHERE fkColumns.OWNER = foreignKeys.OWNER
                            AND fkColumns.CONSTRAINT_NAME = foreignKeys.CONSTRAINT_NAME
                            AND NOT EXISTS (
                                SELECT 1 FROM ALL_CONS_COLUMNS uniqueColumns
                                WHERE uniqueColumns.OWNER = uniqueKeys.OWNER
                                    AND uniqueColumns.CONSTRAINT_NAME = uniqueKeys.CONSTRAINT_NAME
                                    AND uniqueColumns.COLUMN_NAME = fkColumns.COLUMN_NAME
                            )
                    )
            ) THEN 1 ELSE 0 END AS "IsUnique"
        FROM ALL_CONSTRAINTS foreignKeys
        INNER JOIN ALL_CONS_COLUMNS foreignColumns
            ON foreignColumns.OWNER = foreignKeys.OWNER
            AND foreignColumns.CONSTRAINT_NAME = foreignKeys.CONSTRAINT_NAME
        INNER JOIN ALL_CONSTRAINTS primaryKeys
            ON primaryKeys.OWNER = foreignKeys.R_OWNER
            AND primaryKeys.CONSTRAINT_NAME = foreignKeys.R_CONSTRAINT_NAME
        INNER JOIN ALL_CONS_COLUMNS primaryColumns
            ON primaryColumns.OWNER = primaryKeys.OWNER
            AND primaryColumns.CONSTRAINT_NAME = primaryKeys.CONSTRAINT_NAME
            AND primaryColumns.POSITION = foreignColumns.POSITION
        WHERE foreignKeys.CONSTRAINT_TYPE = 'R'
        ORDER BY foreignKeys.CONSTRAINT_NAME, foreignColumns.POSITION
        """;

    public override IDbConnection CreateDbConnection()
        => new OracleConnection(ConnectionString);
}
