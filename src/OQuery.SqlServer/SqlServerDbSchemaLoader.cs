using Microsoft.Data.SqlClient;
using System.Data;

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace OQuery;

public class SqlServerDbSchemaLoader(string connectionString, string[] initializationScripts) : DbSchemaLoader("SqlServer", connectionString, initializationScripts, "[", "]")
{
    protected override string SqlGetColumns => """
        SELECT
            schemas.name AS SchemaName,
            tables.name AS TableName,
            columns.name AS ColumnName,
            types.name AS DataType,
            columns.is_nullable AS IsNullable,
            CAST(CASE WHEN primaryKeys.column_id IS NULL THEN 0 ELSE 1 END AS bit) AS IsPrimaryKey
        FROM sys.tables AS tables
        INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
        INNER JOIN sys.columns AS columns ON columns.object_id = tables.object_id
        INNER JOIN sys.types AS types ON types.user_type_id = columns.user_type_id
        LEFT JOIN sys.indexes AS primaryIndex
            ON primaryIndex.object_id = tables.object_id AND primaryIndex.is_primary_key = 1
        LEFT JOIN sys.index_columns AS primaryKeys
            ON primaryKeys.object_id = tables.object_id
            AND primaryKeys.index_id = primaryIndex.index_id
            AND primaryKeys.column_id = columns.column_id
        ORDER BY schemas.name, tables.name, columns.column_id;
        """;

    protected override string SqlGetRelationships => """
        SELECT
            foreignKeys.name AS RelationName,
            sourceSchemas.name AS SourceSchema,
            sourceTables.name AS SourceTable,
            sourceColumns.name AS SourceColumn,
            targetSchemas.name AS TargetSchema,
            targetTables.name AS TargetTable,
            targetColumns.name AS TargetColumn,
            foreignKeyColumns.constraint_column_id AS Ordinal,
            CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM sys.indexes AS uniqueIndexes
                WHERE uniqueIndexes.object_id = foreignKeyColumns.parent_object_id
                    AND uniqueIndexes.is_unique = 1
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.index_columns AS uniqueColumns
                        WHERE uniqueColumns.object_id = uniqueIndexes.object_id
                            AND uniqueColumns.index_id = uniqueIndexes.index_id
                            AND uniqueColumns.key_ordinal > 0
                            AND NOT EXISTS (
                                SELECT 1
                                FROM sys.foreign_key_columns AS matchingForeignColumns
                                WHERE matchingForeignColumns.constraint_object_id = foreignKeys.object_id
                                    AND matchingForeignColumns.parent_column_id = uniqueColumns.column_id
                            )
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.foreign_key_columns AS requiredForeignColumns
                        WHERE requiredForeignColumns.constraint_object_id = foreignKeys.object_id
                            AND NOT EXISTS (
                                SELECT 1
                                FROM sys.index_columns AS matchingUniqueColumns
                                WHERE matchingUniqueColumns.object_id = uniqueIndexes.object_id
                                    AND matchingUniqueColumns.index_id = uniqueIndexes.index_id
                                    AND matchingUniqueColumns.key_ordinal > 0
                                    AND matchingUniqueColumns.column_id = requiredForeignColumns.parent_column_id
                            )
                    )
            ) THEN 1 ELSE 0 END AS bit) AS IsUnique
        FROM sys.foreign_keys AS foreignKeys
        INNER JOIN sys.foreign_key_columns AS foreignKeyColumns
            ON foreignKeyColumns.constraint_object_id = foreignKeys.object_id
        INNER JOIN sys.tables AS sourceTables ON sourceTables.object_id = foreignKeyColumns.parent_object_id
        INNER JOIN sys.schemas AS sourceSchemas ON sourceSchemas.schema_id = sourceTables.schema_id
        INNER JOIN sys.columns AS sourceColumns
            ON sourceColumns.object_id = foreignKeyColumns.parent_object_id
            AND sourceColumns.column_id = foreignKeyColumns.parent_column_id
        INNER JOIN sys.tables AS targetTables ON targetTables.object_id = foreignKeyColumns.referenced_object_id
        INNER JOIN sys.schemas AS targetSchemas ON targetSchemas.schema_id = targetTables.schema_id
        INNER JOIN sys.columns AS targetColumns
            ON targetColumns.object_id = foreignKeyColumns.referenced_object_id
            AND targetColumns.column_id = foreignKeyColumns.referenced_column_id
        WHERE foreignKeys.is_disabled = 0
        ORDER BY foreignKeys.name, foreignKeyColumns.constraint_column_id;
        """;

    public override IDbConnection CreateDbConnection()
        => new SqlConnection(ConnectionString);
}
