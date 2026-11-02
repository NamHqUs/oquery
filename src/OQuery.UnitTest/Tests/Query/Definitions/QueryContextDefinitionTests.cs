using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;
using System.Text.Json;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
public sealed class QueryContextDefinitionTests : TestBase
{
    [Test]
    public void MultipleContexts_StartEachCteOnANewLine()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserContext",
                    new QueryDefinition { Entity = "User", Projection = [new("Id")] },
                    Condition: null),
                new QueryContextDefintion(
                    "UserContext1",
                    new QueryDefinition { Entity = "User", Projection = [new("Id")] },
                    Condition: null)
            ],
            Projection = [new("Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.StartWith($"WITH{Environment.NewLine}[UserContext] AS ("));
        Assert.That(sql, Does.Contain($"),{Environment.NewLine}[UserContext1] AS ("));
    }

    [Test]
    public void Sample_RendersCteProjectionAndLeftJoin()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity(
                "User",
                [
                    new Field("Id", "Id"),
                    new Field("Name", "Name"),
                    new Field("ManagerId", "ManagerId"),
                ]));

        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    Name: "UserContext",
                    Query: new QueryDefinition
                    {
                        Entity = "User",
                        Projection =
                        [
                            new("Id"),
                            new("Name", "Alias Name"),
                        ],
                    },
                    Condition: new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection =
            [
                new("Id"),
                new("Name", "User Name"),
                new("UserContext.Alias Name", "Manager Name")
            ],
        };

        var sql = db.Query(query).ToSql();

        Assert.That(
            sql.CommandText,
            Is.EqualTo(
                string.Join(
                    Environment.NewLine,
                    "WITH",
                    "[UserContext] AS (",
                    "    SELECT t2.[Id] [Id], t2.[Name] [Alias Name]",
                    "    FROM [dbo].[User] t2)",
                    "SELECT t0.[Id] [Id], t0.[Name] [User Name], t1.[Alias Name] [Manager Name]",
                    "FROM [dbo].[User] t0",
                    "\tLEFT JOIN [UserContext] t1 ON (t0.[ManagerId] = t1.[Id])")));
        Assert.That(sql.Parameters, Is.Empty);
    }

    [Test]
    public void RecursiveReferenceOnlyInJoinCondition_RendersRecursiveCte()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity(
                "User",
                [
                    new Field("Id", "Id"),
                    new Field("ManagerId", "ManagerId"),
                ]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserTree",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Id"), new("ManagerId")],
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")))
                    {
                        Recursive = true
                    }
            ],
            Projection =
            [
                new("Id"),
                new("ManagerId"),
            ]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain("UNION ALL"));
        Assert.That(sql, Does.Contain("[UserTree]"));
        Assert.That(sql, Does.Contain("[Id], [ManagerId]"));
    }

    [Test]
    public void RecursiveDeclarationWithoutSelfReference_IsRejected()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserTree",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Id")],
                    },
                    Condition: null)
                {
                    Recursive = true
                }
            ],
            Projection = [new("Id")]
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<FormatException>());
    }

    [Test]
    public void RecursiveReferenceInJoinCondition_IsDetectedWhenEntitiesDiffer()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]),
            new Entity("UserHierarchy", [new Field("Id", "Id")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserTree",
                    new QueryDefinition
                    {
                        Entity = "UserHierarchy",
                        Projection =
                        [
                            new("Id"),
                            new("UserTree.Id", "Level"),
                        ],
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")))
            ],
            Projection = [new("Id"), new("Id", "Level")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain("UNION ALL"));
    }

    [Test]
    public void RecursiveReferenceInJoinCondition_IsRejectedWithMultipleContexts()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserTree",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection =
                        [
                            new("Id"),
                            new("UserTree.Id", "Level"),
                        ],
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("OtherUsers.Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id"))),
                new QueryContextDefintion(
                    "OtherUsers",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Id")],
                    },
                    Condition: null)
            ],
            Projection = [new("Id")]
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void RecursiveQuery_IsRejectedWhenDialectDoesNotSupportRecursiveCte()
    {
        using var db = CreateContext(
            new SapHanaDialect(),
            new Entity("User", [new Field("Id", "Id")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserTree",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Id"), new("UserTree.Id", "Level")],
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")))
            ],
            Projection = [new("Id"), new("Id", "Level")]
        };

        Assert.That(() => db.Query(query).ToSql(), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void RecursiveQuery_RequiresMatchingAnchorAndRecursiveProjectionShapes()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity(
                "User",
                [
                    new Field("Id", "Id"),
                    new Field("ManagerId", "ManagerId"),
                ]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserTree",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection =
                        [
                            new("Id"),
                            new("ManagerId"),
                            new("UserTree.Id", "Level"),
                        ],
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")))
            ],
            Projection = [new("Id")]
        };

        Assert.That(() => db.Query(query).ToSql(), Throws.TypeOf<FormatException>());
    }

    [TestCase(null)]
    [TestCase(0)]
    public void ContextDoesNotPageWhenPageSizeIsUnsetOrZero(int? pageSize)
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id"), new Field("ManagerId", "ManagerId")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "Manager",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Id")],
                        Page = 1,
                        PageSize = pageSize
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("Manager.Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Not.Contain("OFFSET"));
        Assert.That(sql, Does.Not.Contain("FETCH NEXT"));
        Assert.That(sql, Does.Not.Contain("ORDER BY ("));
    }


    [TestCase("SqlServer")]
    [TestCase("Sqlite")]
    [TestCase("Postgres")]
    public void Contexts_UseDialectIdentifierQuoting(string dialectName)
    {
        ISqlDialect dialect = dialectName switch
        {
            "SqlServer" => new SqlServerDialect(),
            "Sqlite" => new SqliteDialect(),
            "Postgres" => new PostgresDialect(),
            _ => throw new ArgumentOutOfRangeException(nameof(dialectName))
        };
        using var db = CreateContext(
            dialect,
            new Entity("User", [new Field("Id", "Id"), new Field("ManagerId", "ManagerId")]));
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserContext",
                    new QueryDefinition { Entity = "User", Projection = [new("Id")] },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("UserContext.Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain($"{dialect.QuoteIdentifier("UserContext")} AS"));
        Assert.That(sql, Does.Contain($"{dialect.QuoteIdentifier("UserContext")} t1"));
    }

    [Test]
    public void QueryDefinitionYaml_RoundTripsContextsAndJoinCondition()
    {
        const string yaml = """
            Entity: User
            Projection:
              - UserContext.Id | Manager Id
            Contexts:
              - Name: UserContext
                Alias: Manager
                JoinType: Left
                Recursive: true
                Query:
                  Entity: User
                  Projection:
                    - Id
                Condition: ManagerId = UserContext.Id
            """;

        var definition = QueryDefinition.FromYml(yaml);
        var parsedAgain = QueryDefinition.FromYml(definition.ToYml());

        Assert.That(parsedAgain.Contexts, Has.Count.EqualTo(1));
        Assert.That(parsedAgain.Contexts![0].Name, Is.EqualTo("UserContext"));
        Assert.That(parsedAgain.Contexts[0].Alias, Is.EqualTo("Manager"));
        Assert.That(parsedAgain.Contexts[0].JoinType, Is.EqualTo(QueryJoinType.Left));
        Assert.That(parsedAgain.Contexts[0].Recursive, Is.True);
        Assert.That(parsedAgain.Contexts[0].Condition?.ToText(), Is.EqualTo("ManagerId = UserContext.Id"));
        Assert.That(parsedAgain.Projection[0].Alias, Is.EqualTo("Manager Id"));
    }

    [Test]
    public void QueryDefinitionYaml_ParsesAndRoundTripsRangeJoinExpressions()
    {
        const string yaml = """
            Entity: A
            Projection:
              - A.Salary
            Contexts:
              - Name: A
                Query:
                  Entity: Employees
                  Projection:
                    - Salary
              - Name: B
                JoinType: Inner
                Query:
                  Entity: SalaryRules
                  Projection:
                    - MinSalary
                Condition: A.Salary BETWEEN <MinSalary, MinSalary + 10000>
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("Employees", [new Field("Salary", "Salary")]),
            new Entity("SalaryRules", [new Field("MinSalary", "MinSalary")]));

        var definition = QueryDefinition.FromYml(yaml);
        var roundTripped = QueryDefinition.FromYml(definition.ToYml());
        var sql = db.Query(roundTripped).ToSql();

        Assert.That(sql.CommandText, Does.Contain("INNER JOIN [B]"));
        Assert.That(sql.CommandText, Does.Contain("t0.[Salary] BETWEEN t1.[MinSalary] AND (t1.[MinSalary] + @p0)"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 10000 }));
    }

    [Test]
    public void QueryDefinitionJson_ParsesContextsAndJoinCondition()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [{ "fields": ["Id"] }],
              "contexts": [{
                "name": "UserContext",
                "joinType": "left",
                "recursive": true,
                "query": {
                  "entity": "User",
                  "projection": [{ "fields": ["Id"] }]
                },
                "condition": {
                  "left": "ManagerId",
                  "operation": 0,
                  "right": "UserContext.Id"
                }
              }]
            }
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id"), new Field("ManagerId", "ManagerId")]));

        var sql = db.Query(QueryDefinitionParser.Parse(json)).ToSql();

        Assert.That(sql.CommandText, Does.Contain("LEFT JOIN [UserContext]"));
        Assert.That(sql.CommandText, Does.Contain("UNION ALL"));
    }

    [Test]
    public void QueryDefinitionJson_RejectsNonBooleanRecursiveFlag()
    {
        const string json = """
            {
              "entity": "User",
              "contexts": [{
                "name": "UserContext",
                "recursive": "true",
                "query": {
                  "entity": "User",
                  "projection": [{ "fields": ["Id"] }]
                }
              }]
            }
            """;

        Assert.That(
            () => QueryDefinitionParser.Parse(json),
            Throws.TypeOf<JsonException>());
    }

    [Test]
    public void QueryDefinitionJson_ParsesBuiltinOperatorIdsInJoinConditions()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [{ "fields": ["UserContext.Id"] }],
              "contexts": [{
                "name": "UserContext",
                "query": {
                  "entity": "User",
                  "projection": [{ "fields": ["Id"] }]
                },
                "condition": {
                  "left": "ManagerId",
                  "operation": "builtin:4",
                  "right": "Id"
                }
              }]
            }
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id"), new Field("ManagerId", "ManagerId")]));

        var sql = db.Query(QueryDefinitionParser.Parse(json)).ToSql();

        Assert.That(sql.CommandText, Does.Contain("t0.[ManagerId] > t1.[Id]"));
    }

    [Test]
    public void QueryDefinitionJson_DefaultsOmittedJoinOperatorToEquals()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [{ "fields": ["UserContext.Id"] }],
              "contexts": [{
                "name": "UserContext",
                "query": {
                  "entity": "User",
                  "projection": [{ "fields": ["Id"] }]
                },
                "condition": {
                  "left": "ManagerId",
                  "right": "Id"
                }
              }]
            }
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id"), new Field("ManagerId", "ManagerId")]));

        var sql = db.Query(QueryDefinitionParser.Parse(json)).ToSql();

        Assert.That(sql.CommandText, Does.Contain("t0.[ManagerId] = t1.[Id]"));
    }

    [Test]
    public void QueryDefinitionJson_UsesContextAliasesForRepeatedReferences()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [
                { "fields": ["Manager.Name"] },
                { "fields": ["Reviewer.Age"] }
              ],
              "contexts": [
                {
                  "name": "UserContext",
                  "alias": "Manager",
                  "query": {
                    "entity": "User",
                    "projection": [
                      { "fields": ["Id"] },
                      { "fields": ["Name"] },
                      { "fields": ["Age"] }
                    ]
                  },
                  "condition": { "left": "ManagerId", "operation": 0, "right": "Id" }
                },
                {
                  "name": "UserContext",
                  "alias": "Reviewer",
                  "query": {
                    "entity": "User",
                    "projection": [
                      { "fields": ["Id"] },
                      { "fields": ["Name"] },
                      { "fields": ["Age"] }
                    ]
                  },
                  "condition": { "left": "ReviewerId", "operation": 0, "right": "Id" }
                }
              ]
            }
            """;
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [
                new Field("Id", "Id"),
                new Field("Name", "Name"),
                new Field("Age", "Age"),
                new Field("ManagerId", "ManagerId"),
                new Field("ReviewerId", "ReviewerId"),
            ]));

        var sql = db.Query(QueryDefinitionParser.Parse(json)).ToSql().CommandText;

        Assert.That(sql, Does.Contain($"WITH{Environment.NewLine}[UserContext] AS"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sql, @"\[UserContext\] AS").Count, Is.EqualTo(1));
        Assert.That(sql, Does.Contain("LEFT JOIN [UserContext] t1"));
        Assert.That(sql, Does.Contain("LEFT JOIN [UserContext] t2"));
        Assert.That(sql, Does.Contain("t1.[Name] [Manager.Name]"));
        Assert.That(sql, Does.Contain("t2.[Age] [Reviewer.Age]"));
    }

    [Test]
    public void QueryDefinitionWithContexts_CanStillBeComposed()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id"), new Field("ManagerId", "ManagerId")]));
        var definition = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserContext",
                    new QueryDefinition { Entity = "User", Projection = [new("Id")] },
                    new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("UserContext.Id")]
        };

        var sql = db.Query(definition)
            .Where(record => record["Id"] > 0)
            .ToSql();

        Assert.That(sql.CommandText, Does.Contain("WHERE (t0.[Id] > 0)"));
        Assert.That(sql.Parameters, Is.Empty);
    }

    private static OQueryEngine CreateContext(ISqlDialect dialect, params Entity[] entities)
        => new(new SqliteConnection(), new MetadataProvider(entities), dialect);

    private static ProjectionDefinition Projection(string alias, IFieldDefinition expression)
        => new([expression], alias);

    private static FunctionFieldDefinition Function(string name, params IFieldDefinition[] arguments)
        => new(name, arguments);
}
