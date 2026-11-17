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

    private static readonly Entity[] TypedMetadata =
    [
        new(
            Name: "Invoices",
            Fields:
            [
                new Field("Id", "Id"),
                new Field("InvoiceNumber", "InvoiceNumber"),
                new Field("InvoiceDate", "InvoiceDate", DataType.Datetime),
                new Field("DueDate", "DueDate", DataType.Datetime)
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

    [Test]
    public async Task ToNowAndDateRangeFunctionsCountCompletedUnitsOnSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Invoices" (
                    "Id" INTEGER PRIMARY KEY,
                    "InvoiceNumber" TEXT NOT NULL,
                    "InvoiceDate" TEXT NOT NULL,
                    "DueDate" TEXT NOT NULL
                );
                INSERT INTO "Invoices" ("Id", "InvoiceNumber", "InvoiceDate", "DueDate")
                VALUES (1, 'INV-001', date('now', 'localtime', '-5 years'), date('now', 'localtime', '-3 days'));
                """;
            command.ExecuteNonQuery();
        }
        using var context = CreateTypedContext(connection);
        var query = new QueryDefinition("Invoices")
        {
            Projections =
            [
                new(new FunctionFieldDefinition("InvoiceDate.YearsToNow", []), "Years"),
                new(new FunctionFieldDefinition("InvoiceDate.YearsTo", [new FieldDefinition("DueDate")]), "YearsBetween"),
                new(new FunctionFieldDefinition("DueDate.DaysToNow", []), "Days")
            ]
        };

        var record = (await context.Query(query).ToListWithTotalRecordsAsync()).Records.Single();

        Assert.That(Convert.ToInt32(record["Years"]), Is.EqualTo(5));
        Assert.That(Convert.ToInt32(record["Days"]), Is.EqualTo(3));
        Assert.That(Convert.ToInt32(record["YearsBetween"]), Is.EqualTo(4));
    }

    [Test]
    public void MonthsToNowSubtractsIncompleteMonthOnSqlServer()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = new OQueryEngine(connection, new MetadataProvider(TypedMetadata),
            new FunctionProvider(new SqlServerDialect()), new SqlServerDialect());
        var query = CreateReceiverQuery(new FunctionFieldDefinition("InvoiceDate.MonthsToNow", []));

        var sql = context.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Match(@"DATEDIFF\s*\(\s*month,.*GETDATE\s*\(\s*\)"));
        Assert.That(sql, Does.Match(@"CASE WHEN DAY\s*\(\s*GETDATE\s*\(\s*\)\s*\)\s*<\s*DAY\s*\("));
    }

    [Test]
    public void ReceiverFunctionInsideCollectionPredicateResolvesAgainstCollection()
    {
        var sellerMetadata = new Entity[]
        {
            new(
                Name: "Seller",
                Fields:
                [
                    new Field("Id", "Id"),
                    new Field("ManagerId", "ManagerId"),
                    new Field("Salary", "Salary", DataType.Number),
                    new Field("HireDate", "HireDate", DataType.Datetime)
                ],
                Relations:
                [
                    new(
                        Name: "Members",
                        RefEntity: "Seller",
                        Type: RelationType.Collection,
                        RefKeys: [new("Id", "ManagerId")])
                ])
        };
        var definition = QueryDefinitionOQueryParser.Parse(
            "Seller: Id\n  Where Members.Any(Salary > <80000> AND HireDate.DateSub(<2025-10-31>) >= <5>)");
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = new OQueryEngine(connection, new MetadataProvider(sellerMetadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());

        var sql = context.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Match(@"EXISTS"));
        Assert.That(sql, Does.Match(@"julianday"));
    }

    [TestCase("Orders: Id, Any(Items, Quantity >= <5>) HasBulk, NotAny(Items) NoItems")]
    [TestCase("Orders: Id, Items.Any(Quantity >= <5>) HasBulk, Items.NotAny() NoItems")]
    public void ProjectedAnyAndNotAnyRenderCorrelatedExistsInSelectList(string oquery)
    {
        var metadata = new Entity[]
        {
            new(
                Name: "Orders",
                Fields: [new Field("Id", "Id", DataType.Number), new Field("Status", "Status", DataType.String)],
                Relations:
                [
                    new(Name: "Items", RefEntity: "OrderItems", Type: RelationType.Collection,
                        RefKeys: [new("Id", "OrderId")])
                ]),
            new(
                Name: "OrderItems",
                Fields:
                [
                    new Field("Id", "Id", DataType.Number),
                    new Field("OrderId", "OrderId", DataType.Number),
                    new Field("Quantity", "Quantity", DataType.Number)
                ])
        };
        var definition = QueryDefinitionOQueryParser.Parse(oquery);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = new OQueryEngine(connection, new MetadataProvider(metadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());

        var sql = context.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Match(@"SELECT[\s\S]*EXISTS\s*\(\s*SELECT 1\s+FROM[\s\S]*t1\.""OrderId"""));
        Assert.That(sql, Does.Match(@"SELECT[\s\S]*NOT EXISTS\s*\(\s*SELECT 1\s+FROM"));
    }

    [Test]
    public void EntityJoinToSameEntityFiltersMembersHiredBeforeManager()
    {
        var metadata = new Entity[]
        {
            new(
                Name: "Seller",
                Fields:
                [
                    new Field("Id", "Id", DataType.Number),
                    new Field("FirstName", "FirstName", DataType.String),
                    new Field("ManagerId", "ManagerId", DataType.Number),
                    new Field("HireDate", "HireDate", DataType.Datetime)
                ])
        };
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Seller" (
                    "Id" INTEGER PRIMARY KEY,
                    "FirstName" TEXT NOT NULL,
                    "ManagerId" INTEGER NULL,
                    "HireDate" TEXT NOT NULL
                );
                INSERT INTO "Seller" ("Id", "FirstName", "ManagerId", "HireDate") VALUES
                    (1, 'Ann', NULL, '2015-01-01'),
                    (2, 'Ben', 1, '2010-01-01'),
                    (3, 'Cara', 1, '2020-01-01');
                """;
            command.ExecuteNonQuery();
        }
        using var context = new OQueryEngine(connection, new MetadataProvider(metadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());
        var definition = QueryDefinitionOQueryParser.Parse("""
            Seller:
              Join Seller m On Id = m.ManagerId
              Select FirstName ManagerName, HireDate ManagerHireDate,
                m.FirstName MemberName, m.HireDate MemberHireDate
              Where m.HireDate < HireDate
            """);

        var rows = context.Query(definition).ToList();

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0]["MemberName"], Is.EqualTo("Ben"));
    }

    private static OQueryEngine CreateTypedContext(SqliteConnection connection)
    {
        return new OQueryEngine(connection, new MetadataProvider(TypedMetadata),
            new FunctionProvider(new SqliteDialect()), new SqliteDialect());
    }


    private static QueryDefinition CreateReceiverQuery(FunctionFieldDefinition function)
        => new("Invoices") { Projections = [new(function, "Days")] };
}
