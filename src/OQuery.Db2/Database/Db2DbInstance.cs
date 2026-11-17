using IBM.Data.Db2;
using System.Data;

namespace OQuery.Database;

public sealed class Db2DbInstance(string connectionString, string[] scriptFiles, string dbVersion)
    : DbInstance("Db2", connectionString, scriptFiles, dbVersion)
{
    protected override string SqlCreateVersionsTable
        => "CREATE SCHEMA scripts; CREATE TABLE scripts.Versions (Version varchar(255) NOT NULL PRIMARY KEY, CreateOn timestamp NOT NULL DEFAULT CURRENT TIMESTAMP);";

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

    protected override string SqlGetColumns => """
        SELECT
            columns.TABSCHEMA AS SchemaName,
            columns.TABNAME AS TableName,
            columns.COLNAME AS ColumnName,
            columns.TYPENAME AS DataType,
            CASE WHEN columns.NULLS = 'Y' THEN 1 ELSE 0 END AS IsNullable,
            CASE WHEN primaryKeys.COLNAME IS NULL THEN 0 ELSE 1 END AS IsPrimaryKey
        FROM SYSCAT.COLUMNS AS columns
        LEFT JOIN SYSCAT.KEYCOLUSE AS primaryKeys
            ON primaryKeys.TABSCHEMA = columns.TABSCHEMA
            AND primaryKeys.TABNAME = columns.TABNAME
            AND primaryKeys.COLNAME = columns.COLNAME
            AND primaryKeys.CONSTNAME IN (
                SELECT CONSTNAME FROM SYSCAT.TABCONST
                WHERE TABSCHEMA = columns.TABSCHEMA
                    AND TABNAME = columns.TABNAME
                    AND TYPE = 'P')
        WHERE columns.TABSCHEMA NOT LIKE 'SYS%'
        ORDER BY columns.TABSCHEMA, columns.TABNAME, columns.COLNO
        """;

    protected override string SqlGetRelationships => """
        SELECT
            relations.CONSTNAME AS RelationName,
            relations.TABSCHEMA AS SourceSchema,
            relations.TABNAME AS SourceTable,
            relations.COLNAME AS SourceColumn,
            relations.REFTABSCHEMA AS TargetSchema,
            relations.REFTABNAME AS TargetTable,
            relations.REFCOLNAME AS TargetColumn,
            relations.COLSEQ AS Ordinal,
            0 AS IsUnique
        FROM SYSCAT.REFERENCES AS relations
        WHERE relations.TABSCHEMA NOT LIKE 'SYS%'
        ORDER BY relations.CONSTNAME, relations.COLSEQ
        """;

    public override IDbConnection CreateDbConnection()
        => new DB2Connection(ConnectionString);
}
