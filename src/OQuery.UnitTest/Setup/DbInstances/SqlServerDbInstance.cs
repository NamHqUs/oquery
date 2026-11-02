using Microsoft.Data.SqlClient;
using System.Data;

using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Setup.DbInstances;

public class SqlServerDbInstance(string connectionString, string[] scriptFiles, string dbVersion) : DbInstance( connectionString, scriptFiles, dbVersion)
{
    protected override string SqlCreateVersionsTable
        => "IF SCHEMA_ID(N'scripts') IS NULL EXEC(N'CREATE SCHEMA [scripts]'); IF OBJECT_ID(N'[scripts].[Versions]', N'U') IS NULL CREATE TABLE [scripts].[Versions] ([Version] nvarchar(255) NOT NULL PRIMARY KEY, [CreateOn] datetime2 NOT NULL DEFAULT SYSUTCDATETIME());";

    protected override string SqlVersionExists
        => "SELECT CASE WHEN EXISTS (SELECT 1 FROM [scripts].[Versions] WHERE [Version] = @DbVersion) THEN 1 ELSE 0 END;";

    protected override string SqlInsertVersion
        => "INSERT INTO [scripts].[Versions] ([Version]) VALUES (@DbVersion);";

    protected override string SqlCreateTrackedTablesTable
        => "IF OBJECT_ID(N'[scripts].[TrackedTables]', N'U') IS NULL CREATE TABLE [scripts].[TrackedTables] ([TableName] nvarchar(1024) NOT NULL);";

    protected override string SqlReadTrackedTables
        => "SELECT [TableName] FROM [scripts].[TrackedTables];";

    protected override string SqlTruncateTrackedTables
        => "TRUNCATE TABLE [scripts].[TrackedTables];";

    protected override string SqlInsertTrackedTable
        => "INSERT INTO [scripts].[TrackedTables] ([TableName]) VALUES (@TableName);";

    protected override string IdentifierQuotePrefix => "[";
    protected override string IdentifierQuoteSuffix => "]";

    public override IDbConnection CreateDbConnection()
        => GetOrCreateConnection(() => new SqlConnection(ConnectionString));
}
