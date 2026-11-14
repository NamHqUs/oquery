using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Tests.Sql;

[TestFixture]
internal sealed class DateDifferenceQueryTests
{
    private static readonly Entity[] Metadata =
    [
        new(
            Name: "Customers",
            Fields:
            [
                new Field("Id", "Id"),
                new Field("Name", "Name")
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new Field("Id", "Id"),
                new Field("CustomerId", "CustomerId"),
                new Field("InvoiceNumber", "InvoiceNumber"),
                new Field("InvoiceDate", "InvoiceDate"),
                new Field("DueDate", "DueDate")
            ],
            Relations:
            [
                new(
                    Name: "Customer",
                    RefEntity: "Customers",
                    Type: RelationType.Lookup,
                    RefKeys: [new("CustomerId", "Id")])
            ])
    ];

    private static QueryDefinition CreateQuery()
        => new("Invoices")
        {
            Projections =
            [
                new(new FieldDefinition("InvoiceNumber")),
                new(new FieldDefinition("Customer.Name")),
                new(new FieldDefinition("InvoiceDate")),
                new(
                    new FunctionFieldDefinition("DateSub",
                    [
                        new FieldDefinition("DueDate"),
                        new FunctionFieldDefinition("GETDATE", [])
                    ]),
                    "DaysOverdue")
            ],
            Take = 50
        };

    [Test]
    public async Task DateDifferenceProjectionExecutesOnSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Customers" (
                    "Id" INTEGER PRIMARY KEY,
                    "Name" TEXT NOT NULL
                );
                CREATE TABLE "Invoices" (
                    "Id" INTEGER PRIMARY KEY,
                    "CustomerId" INTEGER NOT NULL,
                    "InvoiceNumber" TEXT NOT NULL,
                    "InvoiceDate" TEXT NOT NULL,
                    "DueDate" TEXT NOT NULL
                );
                INSERT INTO "Customers" ("Id", "Name")
                VALUES (1, 'Ada');
                INSERT INTO "Invoices"
                    ("Id", "CustomerId", "InvoiceNumber", "InvoiceDate", "DueDate")
                VALUES
                    (1, 1, 'INV-001', date('now', 'localtime'), date('now', 'localtime', '-3 days'));
                """;
            command.ExecuteNonQuery();
        }

        using var context = new OQueryEngine(
            connection,
            new MetadataProvider(Metadata),
            new FunctionProvider(new SqliteDialect()));
        var queryResult = await context.Query(CreateQuery()).ToListWithTotalRecordsAsync();
        var record = queryResult.Records.Single();

        Assert.That(queryResult.TotalRecords, Is.EqualTo(1));
        Assert.That(record["InvoiceNumber"], Is.EqualTo("INV-001"));
        Assert.That(record["Customer.Name"], Is.EqualTo("Ada"));
        Assert.That(Convert.ToInt32(record["DaysOverdue"]), Is.EqualTo(3));
    }

    [Test]
    public void DateDifferenceProjectionUsesProviderSpecificSql()
    {
        using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        using var sqliteContext = new OQueryEngine(sqliteConnection, new MetadataProvider(Metadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());
        var sqliteSql = sqliteContext.Query(CreateQuery()).ToSql().CommandText;
        Assert.That(sqliteSql, Does.Match(@"julianday\s*\(\s*date"));
        Assert.That(sqliteSql, Does.Match(@"datetime\s*\(\s*'now',\s*'localtime'\s*\)"));

        using var sqlServerConnection = new SqliteConnection("Data Source=:memory:");
        using var sqlServerContext = new OQueryEngine(sqlServerConnection, new MetadataProvider(Metadata),
            new FunctionProvider(new SqlServerDialect()), new SqlServerDialect());
        var sqlServerSql = sqlServerContext.Query(CreateQuery()).ToSql().CommandText;
        Assert.That(sqlServerSql, Does.Match(@"DATEDIFF\s*\(\s*day,"));
        Assert.That(sqlServerSql, Does.Match(@"GETDATE\s*\(\s*\)"));

        using var postgresConnection = new SqliteConnection("Data Source=:memory:");
        using var postgresContext = new OQueryEngine(postgresConnection, new MetadataProvider(Metadata),
            new FunctionProvider(new PostgresDialect()), new PostgresDialect());
        var postgresSql = postgresContext.Query(CreateQuery()).ToSql().CommandText;
        Assert.That(postgresSql, Does.Match(@"CAST\s*\(.* AS DATE\)\s*-\s*CAST\s*\("));
        Assert.That(postgresSql, Does.Contain("CURRENT_TIMESTAMP"));
    }

    [Test]
    public void FieldReceiverFunctionUsesFieldAsFirstArgument()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = CreateTypedContext(connection);

        var receiverSql = context.Query(CreateReceiverQuery(
            new FunctionFieldDefinition("InvoiceDate.DateSub", [new FieldDefinition("DueDate")]))).ToSql().CommandText;
        var explicitSql = context.Query(CreateReceiverQuery(
            new FunctionFieldDefinition("DateSub", [new FieldDefinition("InvoiceDate"), new FieldDefinition("DueDate")]))).ToSql().CommandText;

        Assert.That(receiverSql, Is.EqualTo(explicitSql));
    }

    [Test]
    public void FieldReceiverFunctionRejectsFirstArgumentTypeMismatch()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = CreateTypedContext(connection);
        var query = CreateReceiverQuery(
            new FunctionFieldDefinition("InvoiceNumber.DateSub", [new FieldDefinition("DueDate")]));

        Assert.That(() => context.Query(query).ToSql(),
            Throws.InstanceOf<FormatException>().With.Message.Contains("InvoiceNumber"));
    }

    [Test]
    public void FieldReceiverFunctionInsideCollectionAggregateCompiles()
    {
        var collectionMetadata = new Entity[]
        {
            new(
                Name: "Departments",
                Fields: [new Field("Id", "Id")],
                Relations:
                [
                    new(
                        Name: "Employees",
                        RefEntity: "Employees",
                        Type: RelationType.Collection,
                        RefKeys: [new("Id", "DepartmentId")])
                ]),
            new(
                Name: "Employees",
                Fields:
                [
                    new Field("Id", "Id"),
                    new Field("DepartmentId", "DepartmentId"),
                    new Field("HireDate", "HireDate", DataType.Datetime)
                ])
        };
        var definition = QueryDefinitionOQueryParser.Parse(
            "Departments: Id, Employees.Avg(HireDate.DateSub(<2025-10-31>) / 365.25) AverageTenureYears");
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = new OQueryEngine(connection, new MetadataProvider(collectionMetadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());

        var sql = context.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Match(@"AVG\s*\(.*julianday"));
    }

    private static OQueryEngine CreateTypedContext(SqliteConnection connection)
    {
        var typedMetadata = new Entity[]
        {
            new(
                Name: "Invoices",
                Fields:
                [
                    new Field("Id", "Id"),
                    new Field("InvoiceNumber", "InvoiceNumber"),
                    new Field("InvoiceDate", "InvoiceDate", DataType.Datetime),
                    new Field("DueDate", "DueDate", DataType.Datetime)
                ])
        };
        return new OQueryEngine(connection, new MetadataProvider(typedMetadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());
    }

    private static QueryDefinition CreateReceiverQuery(FunctionFieldDefinition function)
        => new("Invoices") { Projections = [new(function, "Days")] };
}
