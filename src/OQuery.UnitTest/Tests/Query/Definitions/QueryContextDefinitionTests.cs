using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;
using System.Text.Json;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
public sealed class QueryContextDefinitionTests : TestBase
{
    [Test]
    public void OQuerySelfJoinedRootContextGeneratesRecursiveCte()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity(
                "Employees",
                [
                    new Field("Id", "Id"),
                    new Field("FirstName", "FirstName"),
                    new Field("ManagerId", "ManagerId"),
                ]));
        var query = QueryDefinitionOQueryParser.Parse("""
            EmployeeTree is Employees: FirstName | ManagerName,
                member.FirstName | MemberName
              INNER JOIN EmployeeTree member on Id = member.ManagerId
            """);

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain("UNION ALL"));
        Assert.That(sql, Does.Contain("[EmployeeTree]"));
        Assert.That(sql, Does.Contain("[ManagerId] IS NULL"));
        Assert.That(sql, Does.Contain("INNER JOIN [dbo].[Employees] member"));
        Assert.That(sql, Does.Contain("LEFT JOIN [dbo].[Employees] manager"));
        Assert.That(sql, Does.Contain("[FirstName] [ManagerName]"));
        Assert.That(sql, Does.Contain("[FirstName] [MemberName]"));
        Assert.That(
            sql.IndexOf("[ManagerName]", StringComparison.Ordinal),
            Is.LessThan(sql.IndexOf("[MemberName]", StringComparison.Ordinal)));
    }

    [Test]
    public void MultipleContexts_StartEachCteOnANewLine()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]));
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserContext"] = new QueryDefinition("User") {  Projections = [new("Id")] },
                ["UserContext1"] = new QueryDefinition("User") {  Projections = [new("Id")] },
            },
            Joins =
            [
                new JoinDefinition("UserContext", null, null, QueryJoinType.Left),
                new JoinDefinition("UserContext1", null, null, QueryJoinType.Left),
            ],

            Projections = [new("Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.StartWith($"WITH{Environment.NewLine}[UserContext] AS ("));
        Assert.That(sql, Does.Contain($"),{Environment.NewLine}[UserContext1] AS ("));
    }

    [Test]
    public void NestedContextJoinBuildsDependentCtesInDeclarationOrder()
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

        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["ref1"] = new QueryDefinitionBase("User")
                {

                    Projections = [new("Id"), new("Name")]
                },
                ["ref2"] = new QueryDefinitionBase("User")
                {

                    Projections = [new("Id"), new("Name"), new("ManagerId")],
                    Joins =
                    [
                        new("ref1", Condition: QueryDefinitionJsonParser.ParseJoinCondition(
                            "ManagerId = ref1.Id"))
                    ]
                }
            },
            Joins =
            [
                new("ref1", Condition: QueryDefinitionJsonParser.ParseJoinCondition("ManagerId = ref1.Id")),
                new("ref2", Condition: QueryDefinitionJsonParser.ParseJoinCondition("ManagerId = ref2.Id"))
            ],
            Projections =
            [
                new("Id"),
                new("Name"),
                new("ref1.Name", "Manager1"),
                new("ref2.Name", "Manager2")
            ]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain("[ref1] AS"));
        Assert.That(sql, Does.Contain("[ref2] AS"));
        Assert.That(sql, Does.Contain("LEFT JOIN [ref1]"));
        Assert.That(sql, Does.Contain("LEFT JOIN [ref2]"));
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

        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserContext"] = new QueryDefinition("User")
                    {

                        Projections =
                        [
                            new("Id"),
                            new("Name", "Alias Name"),
                        ],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserContext", null, new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")), QueryJoinType.Left),
            ],

            Projections =
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
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id"), new("ManagerId")],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
            ],

            Projections =
            [
                new("Id"),
                new("ManagerId"),
            ]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Not.Contain("UNION ALL"));
        Assert.That(sql, Does.Contain("[UserTree]"));
    }

    [Test]
    public void RecursiveDeclarationWithoutSelfReference_IsRejected()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]));
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id")],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, null, QueryJoinType.Left),
            ],

            Projections = [new("Id")]
        };

        Assert.DoesNotThrow(() => db.Query(query).ToSql());
    }

    [Test]
    public void RecursiveReferenceInJoinCondition_IsDetectedWhenEntitiesDiffer()
    {
        using var db = CreateContext(
            new SqlServerDialect(),
            new Entity("User", [new Field("Id", "Id")]),
            new Entity("UserHierarchy", [new Field("Id", "Id")]));
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("UserHierarchy")
                    {

                        Projections =
                        [
                            new("Id"),
                            new("UserTree.Id", "Level"),
                        ],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
            ],

            Projections = [new("Id"), new("Id", "Level")]
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
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections =
                        [
                            new("Id"),
                            new("UserTree.Id", "Level"),
                        ],
                    },
                ["OtherUsers"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id")],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("OtherUsers.Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
                new JoinDefinition("OtherUsers", null, null, QueryJoinType.Left),
            ],

            Projections = [new("Id")]
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
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id"), new("UserTree.Id", "Level")],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
            ],

            Projections = [new("Id"), new("Id", "Level")]
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
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections =
                        [
                            new("Id"),
                            new("ManagerId"),
                            new("UserTree.Id", "Level"),
                        ],
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
            ],

            Projections = [new("Id")]
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
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["Manager"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id")],
                        Take = pageSize
                    },
            },
            Joins =
            [
                new JoinDefinition("Manager", null, new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")), QueryJoinType.Left),
            ],

            Projections = [new("Manager.Id")]
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
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserContext"] = new QueryDefinition("User") {  Projections = [new("Id")] },
            },
            Joins =
            [
                new JoinDefinition("UserContext", null, new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")), QueryJoinType.Left),
            ],

            Projections = [new("UserContext.Id")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain($"{dialect.QuoteIdentifier("UserContext")} AS"));
        Assert.That(sql, Does.Contain($"{dialect.QuoteIdentifier("UserContext")} t1"));
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

        var sql = db.Query(QueryDefinitionJsonParser.Parse(json)).ToSql();

        Assert.That(sql.CommandText, Does.Contain("LEFT JOIN [UserContext]"));
        Assert.That(sql.CommandText, Does.Not.Contain("UNION ALL"));
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

        Assert.DoesNotThrow(() => QueryDefinitionJsonParser.Parse(json));
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

        var sql = db.Query(QueryDefinitionJsonParser.Parse(json)).ToSql();

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

        var sql = db.Query(QueryDefinitionJsonParser.Parse(json)).ToSql();

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

        var sql = db.Query(QueryDefinitionJsonParser.Parse(json)).ToSql().CommandText;

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
        var definition = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserContext"] = new QueryDefinition("User") {  Projections = [new("Id")] },
            },
            Joins =
            [
                new JoinDefinition("UserContext", null, new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")), QueryJoinType.Left),
            ],

            Projections = [new("UserContext.Id")]
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
