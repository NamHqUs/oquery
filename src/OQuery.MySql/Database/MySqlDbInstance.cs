using System.Data;
using MySqlConnector;

namespace OQuery.Database;

public sealed class MySqlDbInstance(string connectionString, string[] scriptFiles, string dbVersion)
    : DbInstance("MySql", connectionString, scriptFiles, dbVersion)
{
    protected override string SqlCreateVersionsTable
        => "CREATE DATABASE IF NOT EXISTS `scripts`; CREATE TABLE IF NOT EXISTS `scripts`.`Versions` (`Version` varchar(255) NOT NULL PRIMARY KEY, `CreateOn` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP);";

    protected override string SqlVersionExists
        => "SELECT CASE WHEN EXISTS (SELECT 1 FROM `scripts`.`Versions` WHERE `Version` = @DbVersion) THEN 1 ELSE 0 END;";

    protected override string SqlInsertVersion
        => "INSERT INTO `scripts`.`Versions` (`Version`) VALUES (@DbVersion);";

    protected override string SqlCreateTrackedTablesTable
        => "CREATE TABLE IF NOT EXISTS `scripts`.`TrackedTables` (`TableName` varchar(1024) NOT NULL);";

    protected override string SqlReadTrackedTables
        => "SELECT `TableName` FROM `scripts`.`TrackedTables`;";

    protected override string SqlTruncateTrackedTables
        => "TRUNCATE TABLE `scripts`.`TrackedTables`;";

    protected override string SqlInsertTrackedTable
        => "INSERT INTO `scripts`.`TrackedTables` (`TableName`) VALUES (@TableName);";

    protected override string SqlGetColumns => """
        SELECT
            TABLE_SCHEMA AS SchemaName,
            TABLE_NAME AS TableName,
            COLUMN_NAME AS ColumnName,
            COLUMN_TYPE AS DataType,
            CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
            CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS IsPrimaryKey
        FROM information_schema.columns
        WHERE TABLE_SCHEMA = DATABASE()
        ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
        """;

    protected override string SqlGetRelationships => """
        SELECT
            CONCAT(kcu.CONSTRAINT_SCHEMA, '.', kcu.CONSTRAINT_NAME) AS RelationName,
            kcu.TABLE_SCHEMA AS SourceSchema,
            kcu.TABLE_NAME AS SourceTable,
            kcu.COLUMN_NAME AS SourceColumn,
            kcu.REFERENCED_TABLE_SCHEMA AS TargetSchema,
            kcu.REFERENCED_TABLE_NAME AS TargetTable,
            kcu.REFERENCED_COLUMN_NAME AS TargetColumn,
            kcu.ORDINAL_POSITION AS Ordinal,
            CASE WHEN EXISTS (
                SELECT 1
                FROM information_schema.statistics AS uniqueIndexes
                WHERE uniqueIndexes.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                    AND uniqueIndexes.TABLE_NAME = kcu.TABLE_NAME
                    AND uniqueIndexes.NON_UNIQUE = 0
                    AND (SELECT COUNT(*)
                         FROM information_schema.statistics AS uniqueColumns
                         WHERE uniqueColumns.TABLE_SCHEMA = uniqueIndexes.TABLE_SCHEMA
                            AND uniqueColumns.TABLE_NAME = uniqueIndexes.TABLE_NAME
                            AND uniqueColumns.INDEX_NAME = uniqueIndexes.INDEX_NAME)
                        = (SELECT COUNT(*)
                           FROM information_schema.key_column_usage AS foreignColumns
                           WHERE foreignColumns.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
                            AND foreignColumns.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME)
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.key_column_usage AS foreignColumns
                        WHERE foreignColumns.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
                            AND foreignColumns.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                            AND NOT EXISTS (
                                SELECT 1
                                FROM information_schema.statistics AS matchingColumns
                                WHERE matchingColumns.TABLE_SCHEMA = uniqueIndexes.TABLE_SCHEMA
                                    AND matchingColumns.TABLE_NAME = uniqueIndexes.TABLE_NAME
                                    AND matchingColumns.INDEX_NAME = uniqueIndexes.INDEX_NAME
                                    AND matchingColumns.COLUMN_NAME = foreignColumns.COLUMN_NAME
                            )
                    )
            ) THEN 1 ELSE 0 END AS IsUnique
        FROM information_schema.key_column_usage AS kcu
        WHERE kcu.TABLE_SCHEMA = DATABASE()
            AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
        ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION;
        """;

    protected override string IdentifierQuotePrefix => "`";
    protected override string IdentifierQuoteSuffix => "`";

    public override IDbConnection CreateDbConnection()
        => new MySqlConnection(ConnectionString);
}
