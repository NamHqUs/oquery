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
    public async Task JsonDefinitionBuildsCalculatedAggregateProjectionWithFluentGrouping()
    {
        const string json = """
            {
              "entity": "Products",
              "projection": [
                {
                  "field": {
                    "kind": "field",
                    "field": "Name"
                  }
                },
                {
                  "alias": "UnitsSold",
                  "field": {
                    "kind": "function",
                    "function": "SUM",
                    "arguments": [
                      {
                        "kind": "field",
                        "field": "InvoiceItems.Quantity"
                      }
                    ]
                  }
                },
                {
                  "alias": "GrossProfit",
                  "field": {
                    "kind": "function",
                    "function": "SUM",
                    "arguments": [
                      {
                        "kind": "group",
                        "arguments": [
                          {
                            "kind": "field",
                            "field": "InvoiceItems.LineTotal"
                          },
                          {
                            "kind": "group",
                            "arguments": [
                              {
                                "kind": "field",
                                "field": "InvoiceItems.Quantity"
                              },
                              {
                                "oper": "*",
                                "kind": "math",
                                "field": {
                                  "kind": "field",
                                  "field": "CostPrice"
                                }
                              }
                            ],
                            "operator": "-"
                          }
                        ]
                      }
                    ]
                  }
                },
                {
                  "alias": "MarginPercent",
                  "field": {
                    "kind": "function",
                    "function": "ROUND",
                    "arguments": [
                      {
                        "kind": "group",
                        "arguments": [
                          {
                            "kind": "group",
                            "arguments": [
                              {
                                "kind": "function",
                                "function": "SUM",
                                "arguments": [
                                  {
                                    "kind": "group",
                                    "arguments": [
                                      {
                                        "kind": "field",
                                        "field": "InvoiceItems.LineTotal"
                                      },
                                      {
                                        "kind": "group",
                                        "arguments": [
                                          {
                                            "kind": "field",
                                            "field": "InvoiceItems.Quantity"
                                          },
                                          {
                                            "oper": "*",
                                            "kind": "math",
                                            "field": {
                                              "kind": "field",
                                              "field": "CostPrice"
                                            }
                                          }
                                        ],
                                        "operator": "-"
                                      }
                                    ]
                                  }
                                ]
                              },
                              {
                                "oper": "*",
                                "kind": "math",
                                "field": {
                                  "kind": "value",
                                  "value": 100
                                }
                              }
                            ]
                          },
                          {
                            "oper": "/",
                            "kind": "math",
                            "field": {
                              "kind": "function",
                              "function": "NULLIF",
                              "arguments": [
                                {
                                  "kind": "function",
                                  "function": "SUM",
                                  "arguments": [
                                    {
                                      "kind": "field",
                                      "field": "InvoiceItems.LineTotal"
                                    }
                                  ]
                                },
                                {
                                  "kind": "value",
                                  "value": 0
                                }
                              ]
                            }
                          }
                        ]
                      },
                      {
                        "kind": "value",
                        "value": 2
                      }
                    ]
                  }
                }
              ],
              "filter": {
                "logicalOperation": 0,
                "filters": [
                  {
                    "logicalOperator": 0,
                    "field": "InvoiceItems.Invoice.Status",
                    "operation": 0,
                    "value": "Paid",
                    "parameterized": false
                  }
                ]
              },
              "sorts": [
                {
                  "field": "GrossProfit",
                  "direction": 1
                }
              ]
            }
            """;

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
                CREATE TABLE "Invoices" (
                    "Id" INTEGER PRIMARY KEY,
                    "Status" TEXT NOT NULL
                );
                INSERT INTO "Products" ("Id", "Name", "CostPrice") VALUES
                    (1, 'Widget', 3),
                    (2, 'No sales', 4);
                INSERT INTO "Invoices" ("Id", "Status") VALUES
                    (10, 'Paid'),
                    (11, 'Draft');
                INSERT INTO "InvoiceItems" ("Id", "ProductId", "InvoiceId", "Quantity", "LineTotal") VALUES
                    (100, 1, 10, 2, 20),
                    (101, 1, 10, 1, 12),
                    (102, 1, 11, 99, 1000);
                """;
            command.ExecuteNonQuery();
        }
        using var db = new OQueryEngine(connection, Metadata);

        var definition = JsonSerializer.Deserialize<QueryDefinition>(
            json,
            JsonQueryDefinitionParser.CreateSerializerOptions())
            ?? throw new InvalidOperationException("The query request JSON did not produce a definition.");
        var query = db.Query(definition).GroupBy("Id", "Name");
        var sql = query.ToSql();

        Assert.That(sql.CommandText, Does.Match(@"SUM\s*\(\s*\(t1\.""LineTotal"" - \(t1\.""Quantity"" \* t0\.""CostPrice""\)\)\)"));
        Assert.That(sql.CommandText, Does.Match(@"NULLIF\s*\(\s*SUM\s*\(t1\.""LineTotal""\), @p"));
        Assert.That(sql.CommandText, Does.Contain("GROUP BY t0.\"Id\", t0.\"Name\""));
        Assert.That(sql.CommandText, Does.Contain("ORDER BY \"GrossProfit\" DESC"));
        Assert.That(sql.CommandText, Does.Match(@"ORDER BY[\s\S]*DESC"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value),
            Is.EquivalentTo(new object?[] { 100m, 0m, 2m }));
        var definitionText = YamlQueryDefinitionParser.Write(definition);
        Assert.That(definitionText, Does.Not.Contain("GroupBy"));
        Assert.That(definitionText, Does.Contain("ROUND("));

        var queryResult = await query.ToListWithTotalRecordsAsync();
        var records = queryResult.Records;

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(queryResult.TotalRecords, Is.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Widget"));
        Assert.That(Convert.ToDecimal(records[0]["UnitsSold"]), Is.EqualTo(3m));
        Assert.That(Convert.ToDecimal(records[0]["GrossProfit"]), Is.EqualTo(23m));
        Assert.That(Convert.ToDecimal(records[0]["MarginPercent"]), Is.EqualTo(71.88m).Within(0.01m));
    }

    [Test]
    public void FlatAdditiveGroupBuildsArithmeticSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var definition = new QueryDefinition
        {
            Entity = "Products",
            Projection =
            [
                new ProjectionDefinition(
                    QueryDefinitionParser.ParseExpression("Id + CostPrice + Id - CostPrice"),
                    "Calculated")
            ]
        };

        var sql = db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Match(
            @"\(\s*\(\s*\(\s*\(t0\.""Id"" \+ t0\.""CostPrice""\)\s*\+\s*t0\.""Id""\)\s*-\s*t0\.""CostPrice""\)"));
    }

    [Test]
    public void MultiplicationRetainsPrecedenceInsideAdditiveGroup()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var options = JsonQueryDefinitionParser.CreateSerializerOptions();
        var definition = new QueryDefinition
        {
            Entity = "Products",
            Projection =
            [
                new ProjectionDefinition(
                    QueryDefinitionParser.ParseExpression("Id + CostPrice * Id - CostPrice"),
                    "Calculated")
            ]
        };
        var serialized = JsonSerializer.Serialize(definition, options);
        var roundTripped = JsonSerializer.Deserialize<QueryDefinition>(serialized, options)
            ?? throw new InvalidOperationException("The serialized query request did not produce a definition.");
        var fields = roundTripped.Projection[0].Fields;
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
        var projection = new ProjectionDefinition([new FieldDefinition("Name")], "ProductName");

        var defaultOptions = new JsonSerializerOptions();
        JsonQueryDefinitionParser.Configure(defaultOptions);
        var defaultJson = JsonSerializer.Serialize(projection, defaultOptions);
        var webJson = JsonSerializer.Serialize(
            projection,
            JsonQueryDefinitionParser.CreateSerializerOptions());

        Assert.That(defaultJson, Is.EqualTo(
            """{"fields":[{"kind":"field","field":"Name"}],"alias":"ProductName"}"""));
        Assert.That(webJson, Is.EqualTo(
            """{"fields":[{"kind":"field","field":"Name"}],"alias":"ProductName"}"""));
    }

    [Test]
    public void ProjectionSerializationStoresOperatorsOnFields()
    {
        var projection = new ProjectionDefinition(
            [
                new FieldDefinition("CostPrice"),
                new ConstFieldDefinition(MatchOperation.Multiply, 2)
            ],
            "Calculated");
        var options = JsonQueryDefinitionParser.CreateSerializerOptions();

        var serialized = JsonSerializer.Serialize(projection, options);

        Assert.That(serialized, Is.EqualTo(
            """{"fields":[{"kind":"field","field":"CostPrice"},{"oper":"*","kind":"value","value":2}],"alias":"Calculated"}"""));
        var roundTripped = JsonSerializer.Deserialize<ProjectionDefinition>(serialized, options);
        Assert.That(roundTripped?.ToText(), Is.EqualTo(projection.ToText()));
    }

    [Test]
    public void GroupOperatorSurvivesJsonRoundTripAndBuildsSql()
    {
        const string json = """
            {
              "entity": "Products",
              "projection": [{
                "alias": "Calculated",
                "field": {
                  "kind": "group",
                  "operator": "*",
                  "arguments": [
                    { "kind": "field", "field": "Id" },
                    { "kind": "field", "field": "CostPrice" }
                  ]
                }
              }]
            }
            """;
        var options = JsonQueryDefinitionParser.CreateSerializerOptions();
        var definition = JsonSerializer.Deserialize<QueryDefinition>(json, options)
            ?? throw new InvalidOperationException("The query request JSON did not produce a definition.");
        var serialized = JsonSerializer.Serialize(definition, options);
        var roundTripped = JsonSerializer.Deserialize<QueryDefinition>(serialized, options)
            ?? throw new InvalidOperationException("The serialized query request did not produce a definition.");

        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var sql = db.Query(roundTripped).ToSql();

        Assert.That(sql.CommandText, Does.Contain("(t0.\"Id\" * t0.\"CostPrice\")"));
    }

    [Test]
    public void QueryExpressionRejectsUnsupportedFunctions()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var db = new OQueryEngine(connection, Metadata);
        var request = new QueryDefinition
        {
            Entity = "Products",
            Projection =
            [
                new ProjectionDefinition(
                    [new FunctionFieldDefinition(
                        "RANDOM_SQL",
                        [new ConstFieldDefinition(1)])],
                    "Unsafe")
            ]
        };

        Assert.Throws<FormatException>(() => db.Query(request).ToSql());
    }
}
