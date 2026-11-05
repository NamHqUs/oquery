using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;
using System.Text.Json;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
public sealed class QueryExpressionTests
{
    private static readonly Entity[] Metadata =
    [
        new(
            Name: "Products",
            Fields:
            [
                new Field("Id", "Id"),
                new Field("Name", "Name"),
                new Field("CostPrice", "CostPrice")
            ],
            Relations:
            [
                new(
                    Name: "InvoiceItems",
                    RefEntity: "InvoiceItems",
                    Type: RelationType.Collection,
                    RefKeys: [new("Id", "ProductId")])
            ]),
        new(
            Name: "InvoiceItems",
            Fields:
            [
                new Field("Id", "Id"),
                new Field("ProductId", "ProductId"),
                new Field("InvoiceId", "InvoiceId"),
                new Field("Quantity", "Quantity"),
                new Field("LineTotal", "LineTotal")
            ],
            Relations:
            [
                new(
                    Name: "Invoice",
                    RefEntity: "Invoices",
                    Type: RelationType.Lookup,
                    RefKeys: [new("InvoiceId", "Id")])
            ]),
        new(
            Name: "Invoices",
            Fields:
            [
                new Field("Id", "Id"),
                new Field("Status", "Status")
            ])
    ];

    [Test]
    public void CollectionAggregateFunctionsWorkWithoutAggregateMetadataFields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Products" (
                    "Id" INTEGER PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "CostPrice" NUMERIC NOT NULL
                );
                CREATE TABLE "InvoiceItems" (
                    "Id" INTEGER PRIMARY KEY,
                    "ProductId" INTEGER NOT NULL,
                    "InvoiceId" INTEGER NOT NULL,
                    "Quantity" NUMERIC NOT NULL,
                    "LineTotal" NUMERIC NOT NULL
                );
                INSERT INTO "Products" ("Id", "Name", "CostPrice") VALUES
                    (1, 'Widget', 3),
                    (2, 'No sales', 4);
                INSERT INTO "InvoiceItems" ("Id", "ProductId", "InvoiceId", "Quantity", "LineTotal") VALUES
                    (100, 1, 10, 2, 20),
                    (101, 1, 10, 1, 12);
                """;
            command.ExecuteNonQuery();
        }

        using var db = new OQueryEngine(connection, Metadata);
        var records = db.Query("Products")
            .Where(record => record["InvoiceItems.COUNT(Id)"] >= 2)
            .Select("Name", "InvoiceItems.COUNT(Id)")
            .ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Widget"));
        Assert.That(Convert.ToInt32(records[0]["InvoiceItems.COUNT(Id)"]), Is.EqualTo(2));
    }

    [Test]
    public void FlatAdditiveGroupBuildsArithmeticSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var definition = new QueryDefinition("Products")
        {

            Projections =
            [
                new ProjectionDefinition(
                    new GroupFieldDefintion(
                        QueryDefinitionJsonParser.ParseExpression("Id + CostPrice + Id - CostPrice")),
                    "Calculated")
            ]
        };

        var sql = db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Match(
            @"\(\s*\(\s*\(\s*\(t0\.""Id"" \+ t0\.""CostPrice""\)\s*\+\s*t0\.""Id""\)\s*-\s*t0\.""CostPrice""\)"));
    }

    [Test]
    public void StringAdditiveGroupUsesStringConcatenation()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var definition = new QueryDefinition("Products")
        {
            Projections =
            [
                new ProjectionDefinition(
                    new GroupFieldDefintion(
                        QueryDefinitionJsonParser.ParseExpression("Name + < > + Name")),
                    "FullName")
            ]
        };

        var sql = db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("||"));
    }

    [Test]
    public void MultiplicationRetainsPrecedenceInsideAdditiveGroup()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var options = QueryDefinitionJsonParser.CreateSerializerOptions();
        var definition = new QueryDefinition("Products")
        {

            Projections =
            [
                new ProjectionDefinition(
                    new GroupFieldDefintion(
                        QueryDefinitionJsonParser.ParseExpression("Id + CostPrice * Id - CostPrice")),
                    "Calculated")
            ]
        };
        var serialized = JsonSerializer.Serialize(definition, options);
        var roundTripped = QueryDefinitionJsonParser.Parse(serialized);
        var fields = ((GroupFieldDefintion)roundTripped.Projections[0].Field).Fields;
        Assert.That(fields, Has.Length.EqualTo(4));
        Assert.That(fields[1], Is.EqualTo(new FieldDefinition("CostPrice")));
        Assert.That(fields[2], Is.EqualTo(new FieldDefinition(MatchOperation.Multiply, "Id")));
        Assert.That(fields[3], Is.EqualTo(new FieldDefinition(MatchOperation.Subtract, "CostPrice")));

        var sql = db.Query(roundTripped).ToSql();

        Assert.That(sql.CommandText, Does.Match(
            @"t0\.""Id"" \+ \(t0\.""CostPrice"" \* t0\.""Id""\)\s*\)\s*-\s*t0\.""CostPrice"""));
    }

    [Test]
    public void ProjectionSerializationPreservesOriginalStructureAndNamingPolicy()
    {
        var projection = new ProjectionDefinition(new FieldDefinition("Name"), "ProductName");

        var defaultOptions = new JsonSerializerOptions();
        QueryDefinitionJsonParser.Configure(defaultOptions);
        var defaultJson = JsonSerializer.Serialize(projection, defaultOptions);
        var webJson = JsonSerializer.Serialize(
            projection,
            QueryDefinitionJsonParser.CreateSerializerOptions());

        Assert.That(defaultJson, Is.EqualTo(
            """{"field":{"kind":"field","field":"Name"},"alias":"ProductName"}"""));
        Assert.That(webJson, Is.EqualTo(
            """{"field":{"kind":"field","field":"Name"},"alias":"ProductName"}"""));
    }

    [Test]
    public void ProjectionSerializationStoresOperatorsOnFields()
    {
        var projection = new ProjectionDefinition(
            new GroupFieldDefintion(
            [
                new FieldDefinition("CostPrice"),
                new ConstFieldDefinition(MatchOperation.Multiply, 2)
            ]),
            "Calculated");
        var options = QueryDefinitionJsonParser.CreateSerializerOptions();

        var serialized = JsonSerializer.Serialize(projection, options);

        Assert.That(serialized, Is.EqualTo(
            """{"field":{"kind":"group","fields":[{"kind":"field","field":"CostPrice"},{"oper":"*","kind":"value","value":2}]},"alias":"Calculated"}"""));
        var roundTripped = JsonSerializer.Deserialize<ProjectionDefinition>(serialized, options);
        Assert.That(roundTripped?.ToText(), Is.EqualTo(projection.ToText()));
    }

    [Test]
    public void QueryExpressionRejectsUnsupportedFunctions()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var request = new QueryDefinition("Products")
        {

            Projections =
            [
                new ProjectionDefinition(
                    new FunctionFieldDefinition(
                        "RANDOM_SQL",
                        [new ConstFieldDefinition(1)]),
                    "Unsafe")
            ]
        };

        Assert.Throws<FormatException>(() => db.Query(request).ToSql());
    }
}
