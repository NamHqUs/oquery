using Npgsql;
using System.Data;

namespace OQuery.Database;

public sealed class CockroachDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance("Cockroach", connectionString, scriptFiles, dbVersion)
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

    protected override string SqlGetColumns => """
        SELECT
            columns.table_schema AS "SchemaName",
            columns.table_name AS "TableName",
            columns.column_name AS "ColumnName",
            columns.data_type AS "DataType",
            (columns.is_nullable = 'YES') AS "IsNullable",
            (primaryKeys.column_name IS NOT NULL) AS "IsPrimaryKey"
        FROM information_schema.columns AS columns
        LEFT JOIN (
            SELECT keyColumns.table_schema, keyColumns.table_name, keyColumns.column_name
            FROM information_schema.table_constraints AS constraints
            INNER JOIN information_schema.key_column_usage AS keyColumns
                ON keyColumns.constraint_catalog = constraints.constraint_catalog
                AND keyColumns.constraint_schema = constraints.constraint_schema
                AND keyColumns.constraint_name = constraints.constraint_name
            WHERE constraints.constraint_type = 'PRIMARY KEY'
        ) AS primaryKeys
            ON primaryKeys.table_schema = columns.table_schema
            AND primaryKeys.table_name = columns.table_name
            AND primaryKeys.column_name = columns.column_name
        WHERE columns.table_schema <> 'information_schema'
            AND columns.table_schema NOT LIKE 'pg\\_%' ESCAPE '\\'
        ORDER BY columns.table_schema, columns.table_name, columns.ordinal_position;
        """;

    protected override string SqlGetRelationships => """
        SELECT
            foreignKeys.constraint_schema || '.' || foreignKeys.constraint_name AS "RelationName",
            foreignKeys.table_schema AS "SourceSchema",
            foreignKeys.table_name AS "SourceTable",
            foreignKeys.column_name AS "SourceColumn",
            referencedKeys.table_schema AS "TargetSchema",
            referencedKeys.table_name AS "TargetTable",
            referencedKeys.column_name AS "TargetColumn",
            foreignKeys.ordinal_position AS "Ordinal",
            EXISTS (
                SELECT 1
                FROM information_schema.table_constraints AS uniqueConstraints
                WHERE uniqueConstraints.table_schema = foreignKeys.table_schema
                    AND uniqueConstraints.table_name = foreignKeys.table_name
                    AND uniqueConstraints.constraint_type IN ('PRIMARY KEY', 'UNIQUE')
                    AND (
                        SELECT count(*)
                        FROM information_schema.key_column_usage AS uniqueColumns
                        WHERE uniqueColumns.constraint_catalog = uniqueConstraints.constraint_catalog
                            AND uniqueColumns.constraint_schema = uniqueConstraints.constraint_schema
                            AND uniqueColumns.constraint_name = uniqueConstraints.constraint_name
                    ) = (
                        SELECT count(*)
                        FROM information_schema.key_column_usage AS allForeignColumns
                        WHERE allForeignColumns.constraint_catalog = foreignKeys.constraint_catalog
                            AND allForeignColumns.constraint_schema = foreignKeys.constraint_schema
                            AND allForeignColumns.constraint_name = foreignKeys.constraint_name
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.key_column_usage AS allForeignColumns
                        WHERE allForeignColumns.constraint_catalog = foreignKeys.constraint_catalog
                            AND allForeignColumns.constraint_schema = foreignKeys.constraint_schema
                            AND allForeignColumns.constraint_name = foreignKeys.constraint_name
                            AND NOT EXISTS (
                                SELECT 1
                                FROM information_schema.key_column_usage AS uniqueColumns
                                WHERE uniqueColumns.constraint_catalog = uniqueConstraints.constraint_catalog
                                    AND uniqueColumns.constraint_schema = uniqueConstraints.constraint_schema
                                    AND uniqueColumns.constraint_name = uniqueConstraints.constraint_name
                                    AND uniqueColumns.column_name = allForeignColumns.column_name
                            )
                    )
            ) AS "IsUnique"
        FROM information_schema.key_column_usage AS foreignKeys
        INNER JOIN information_schema.table_constraints AS constraints
            ON constraints.constraint_catalog = foreignKeys.constraint_catalog
            AND constraints.constraint_schema = foreignKeys.constraint_schema
            AND constraints.constraint_name = foreignKeys.constraint_name
        INNER JOIN information_schema.constraint_column_usage AS referencedKeys
            ON referencedKeys.constraint_catalog = foreignKeys.constraint_catalog
            AND referencedKeys.constraint_schema = foreignKeys.constraint_schema
            AND referencedKeys.constraint_name = foreignKeys.constraint_name
            AND referencedKeys.ordinal_position = foreignKeys.position_in_unique_constraint
        WHERE constraints.constraint_type = 'FOREIGN KEY'
            AND foreignKeys.table_schema <> 'information_schema'
            AND foreignKeys.table_schema NOT LIKE 'pg\\_%' ESCAPE '\\'
        ORDER BY foreignKeys.constraint_name, foreignKeys.ordinal_position;
        """;

    public override IDbConnection CreateDbConnection()
        => new NpgsqlConnection(ConnectionString);
}
