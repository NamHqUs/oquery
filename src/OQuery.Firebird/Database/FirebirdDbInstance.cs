using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace OQuery.Database;

public sealed class FirebirdDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance("Firebird", connectionString, scriptFiles, dbVersion)
{
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

    protected override string SqlGetColumns => """
        SELECT
            '' AS "SchemaName",
            TRIM(relations.RDB$RELATION_NAME) AS "TableName",
            TRIM(fields.RDB$FIELD_NAME) AS "ColumnName",
            TRIM(fieldTypes.RDB$TYPE_NAME) AS "DataType",
            CAST(CASE WHEN COALESCE(fields.RDB$NULL_FLAG, dataFields.RDB$NULL_FLAG, 0) = 0 THEN 1 ELSE 0 END AS SMALLINT) AS "IsNullable",
            CAST(CASE WHEN primarySegments.RDB$FIELD_NAME IS NULL THEN 0 ELSE 1 END AS SMALLINT) AS "IsPrimaryKey"
        FROM RDB$RELATIONS AS relations
        INNER JOIN RDB$RELATION_FIELDS AS fields
            ON fields.RDB$RELATION_NAME = relations.RDB$RELATION_NAME
        INNER JOIN RDB$FIELDS AS dataFields
            ON dataFields.RDB$FIELD_NAME = fields.RDB$FIELD_SOURCE
        LEFT JOIN RDB$TYPES AS fieldTypes
            ON fieldTypes.RDB$TYPE = dataFields.RDB$FIELD_TYPE
            AND fieldTypes.RDB$FIELD_NAME = 'RDB$FIELD_TYPE'
        LEFT JOIN RDB$RELATION_CONSTRAINTS AS primaryConstraints
            ON primaryConstraints.RDB$RELATION_NAME = relations.RDB$RELATION_NAME
            AND primaryConstraints.RDB$CONSTRAINT_TYPE = 'PRIMARY KEY'
        LEFT JOIN RDB$INDEX_SEGMENTS AS primarySegments
            ON primarySegments.RDB$INDEX_NAME = primaryConstraints.RDB$INDEX_NAME
            AND primarySegments.RDB$FIELD_NAME = fields.RDB$FIELD_NAME
        WHERE COALESCE(relations.RDB$SYSTEM_FLAG, 0) = 0
            AND relations.RDB$VIEW_BLR IS NULL
        ORDER BY relations.RDB$RELATION_NAME, fields.RDB$FIELD_POSITION;
        """;

    protected override string SqlGetRelationships => """
        SELECT
            TRIM(foreignConstraints.RDB$CONSTRAINT_NAME) AS "RelationName",
            '' AS "SourceSchema",
            TRIM(foreignConstraints.RDB$RELATION_NAME) AS "SourceTable",
            TRIM(foreignSegments.RDB$FIELD_NAME) AS "SourceColumn",
            '' AS "TargetSchema",
            TRIM(primaryConstraints.RDB$RELATION_NAME) AS "TargetTable",
            TRIM(primarySegments.RDB$FIELD_NAME) AS "TargetColumn",
            foreignSegments.RDB$FIELD_POSITION AS "Ordinal",
            CAST(CASE WHEN uniqueConstraints.RDB$CONSTRAINT_NAME IS NULL THEN 0 ELSE 1 END AS SMALLINT) AS "IsUnique"
        FROM RDB$RELATION_CONSTRAINTS AS foreignConstraints
        INNER JOIN RDB$REF_CONSTRAINTS AS refConstraints
            ON refConstraints.RDB$CONSTRAINT_NAME = foreignConstraints.RDB$CONSTRAINT_NAME
        INNER JOIN RDB$RELATION_CONSTRAINTS AS primaryConstraints
            ON primaryConstraints.RDB$CONSTRAINT_NAME = refConstraints.RDB$CONST_NAME_UQ
        INNER JOIN RDB$INDEX_SEGMENTS AS foreignSegments
            ON foreignSegments.RDB$INDEX_NAME = foreignConstraints.RDB$INDEX_NAME
        INNER JOIN RDB$INDEX_SEGMENTS AS primarySegments
            ON primarySegments.RDB$INDEX_NAME = primaryConstraints.RDB$INDEX_NAME
            AND primarySegments.RDB$FIELD_POSITION = foreignSegments.RDB$FIELD_POSITION
        LEFT JOIN RDB$RELATION_CONSTRAINTS AS uniqueConstraints
            ON uniqueConstraints.RDB$RELATION_NAME = foreignConstraints.RDB$RELATION_NAME
            AND uniqueConstraints.RDB$CONSTRAINT_TYPE IN ('UNIQUE', 'PRIMARY KEY')
            AND uniqueConstraints.RDB$INDEX_NAME = foreignConstraints.RDB$INDEX_NAME
        WHERE foreignConstraints.RDB$CONSTRAINT_TYPE = 'FOREIGN KEY'
        ORDER BY foreignConstraints.RDB$CONSTRAINT_NAME, foreignSegments.RDB$FIELD_POSITION;
        """;

    public override IDbConnection CreateDbConnection()
        => new FbConnection(ConnectionString);
}
