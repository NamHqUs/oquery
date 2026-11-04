using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Tests.Metadata;

[TestFixture]
public sealed class MetadataAuthorizationPathTests
{
    [Test]
    public void JoinedProjectionRequiresPermissionForTheJoinedField()
    {
        using var db = CreateDatabase();

        Assert.That(
            () => db.Query("User").Select("Name", "Members.Secret").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }
    [Test]
    public void JoinedAggregateRequiresPermissionForItsField()
    {
        using var db = CreateDatabase();

        Assert.That(
            () => db.Query("User").Select("Name", "Members.COUNT(Secret)").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }
    [Test]
    public void ProjectionAliasDoesNotGrantAccessToAHiddenPhysicalField()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
            [new MetadataFieldPermission("User", ["Id"])],
            accessMode: AccessMode.Allow));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("User")
            .Select(new Dictionary<string, object?> { ["Secret"] = "Id" })
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("Id"));
        Assert.That(sql, Does.Not.Contain("t0.\"Secret\""));
    }

    [Test]
    public void AuthorizedRelationPathAndProjectionAliasAreRendered()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [new Field("Id"), new Field("Name")],
                Relations:
                [
                    new Relation(
                        "Members",
                        "Member",
                        RelationType.Collection,
                        [new RelationItem("Id", "UserId")])
                ]),
            new Entity("Member", [new Field("Id"), new Field("UserId"), new Field("Name")])
        ]);
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        var sql = db.Query("User")
            .Select(new Dictionary<string, object?> { ["MemberName"] = "Members.Name" })
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("MemberName"));
        Assert.That(sql, Does.Contain("Name"));
    }

    [Test]
    public void JoinedWildcardProjectionUsesAuthorizedJoinedFields()
    {
        using var db = CreateDatabase();

        var sql = db.Query("User").Select("Members.*").ToSql().CommandText;

        Assert.That(sql, Does.Contain("Id"));
        Assert.That(sql, Does.Not.Contain("Secret"));
    }

    [Test]
    public void CteWildcardProjectionUsesAuthorizedProjectedFields()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id"), new Field("Name"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
            [new MetadataFieldPermission("User", ["Id", "Name"])],
            accessMode: AccessMode.Allow));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserContext"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id"), new("Name")]
                    },
            },
            Joins =
            [
                new JoinDefinition("UserContext", null, null, QueryJoinType.Left),
            ],

            Projections = [new("UserContext.*")]
        };

        var sql = db.Query(query).ToSql().CommandText;

        Assert.That(sql, Does.Contain("Id"));
        Assert.That(sql, Does.Contain("Name"));
        Assert.That(sql, Does.Not.Contain("Secret"));
    }

    [Test]
    public void RawCalculatedFieldsAreCheckedAcrossQueryClauses()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [
                    new Field("Id"),
                    new Field("Value"),
                    new Field("Calculated", "@Value + 1", IsRawSql: true)
                ])
        ]);
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        Assert.Multiple(() =>
        {
            Assert.That(
                () => db.Query("User").Where(user => user["Calculated"] > 0).ToSql(),
                Throws.Nothing);
            Assert.That(
                () => db.Query("User").OrderBy("Calculated").ToSql(),
                Throws.Nothing);
            Assert.That(
                () => db.Query("User").GroupBy("Calculated").ToSql(),
                Throws.Nothing);
            Assert.That(
                () => db.Query("User").Select("COUNT(Calculated)").ToSql(),
                Throws.Nothing);
            Assert.That(
                () => db.Query(new QueryDefinition("User")
                {

                    Projections = QueryDefinitionParser.ParseProjection("COUNT(Calculated) | Total"),
                    Filter = QueryDefinitionParser.ParseFilter("COUNT(Calculated) >= <1>")
                }).ToSql(),
                Throws.Nothing);
        });
    }

    [Test]
    public void FluentCountWildcardIsRejected()
    {
        using var db = CreateDatabase();

        Assert.That(
            () => db.Query("User").Count("*"),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void FluentCountAsyncWildcardIsRejected()
    {
        using var db = CreateDatabase();

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await db.Query("User").CountAsync("*"));
    }

    [Test]
    public void CteProjectionCannotBypassFieldAuthorization()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
            [new MetadataFieldPermission("User", ["Id"])],
            accessMode: AccessMode.Allow));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserContext"] = new QueryDefinition("User")
                    {

                        Projections = [new("Secret")]
                    },
            },
            Joins =
            [
                new JoinDefinition("UserContext", null, null, QueryJoinType.Left),
            ],

            Projections = [new("Id")]
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void UnauthorizedFilterAndSortFieldsAreRejected()
    {
        var metadata = RestrictedUserMetadata();
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        Assert.Multiple(() =>
        {
            Assert.That(
                () => db.Query("User")
                    .Where(user => user["Secret"] == "hidden")
                    .ToSql(),
                Throws.TypeOf<UnauthorizedAccessException>());
            Assert.That(
                () => db.Query("User").OrderBy("Secret").ToSql(),
                Throws.TypeOf<UnauthorizedAccessException>());
        });
    }

    [Test]
    public void UnauthorizedHavingFieldIsRejected()
    {
        var metadata = RestrictedUserMetadata();
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Projections = [new("Id"), new("COUNT(Id) | Total")],
            Filter = QueryDefinitionParser.ParseFilter("COUNT(Secret) >= <1>")
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void UnauthorizedJoinConditionFieldIsRejected()
    {
        var metadata = RestrictedRelationMetadata();
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["MemberContext"] = new QueryDefinition("Member")
                    {

                        Projections = [new("Id")]
                    },
            },
            Joins =
            [
                new JoinDefinition("MemberContext", null, new ConditionStatementDefinition(
                        new FieldDefinition("Secret"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")), QueryJoinType.Left),
            ],

            Projections = [new("Id")]
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void MultiHopRelationAggregateRequiresTheFinalFieldPermission()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [new Field("Id")],
                Relations:
                [
                    new Relation(
                        "Members",
                        "Member",
                        RelationType.Collection,
                        [new RelationItem("Id", "UserId")])
                ]),
            new Entity(
                "Member",
                [new Field("Id"), new Field("UserId")],
                Relations:
                [
                    new Relation(
                        "Tags",
                        "Tag",
                        RelationType.Collection,
                        [new RelationItem("Id", "MemberId")])
                ]),
            new Entity("Tag", [new Field("Id"), new Field("MemberId"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
        [
            new MetadataFieldPermission("User", ["Id"]),
            new MetadataFieldPermission("Member", ["Id"]),
            new MetadataFieldPermission("Tag", ["Id", "MemberId"])
        ],
        accessMode: AccessMode.Allow));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());

        Assert.That(
            () => db.Query("User").Select("Members.Tags.COUNT(Secret)").ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public async Task RawCalculatedFieldWorksThroughAsyncAndTotalQueries()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE User (Id INTEGER PRIMARY KEY, Name TEXT);
                INSERT INTO User (Id, Name) VALUES (1, 'Alice');
                """;
            command.ExecuteNonQuery();
        }

        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [
                    new Field("Id"),
                    new Field("Name"),
                    new Field("Display", "@Name", IsRawSql: true)
                ])
        ]);
        using var db = new OQueryEngine(connection, metadata, new SqliteDialect());
        var query = db.Query("User").Select("Display");

        Assert.That(await query.ToListAsync(), Has.Count.EqualTo(1));
        var (records, total) = await query.ToListWithTotalRecordsAsync();
        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public void CountWildcardInQueryDefinitionIsRejected()
    {
        var metadata = RestrictedUserMetadata();
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Projections = QueryDefinitionParser.ParseProjection("COUNT(*) | Total")
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void RecursiveCteCannotUseDeniedSourceFields()
    {
        var metadata = RestrictedUserMetadata();
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id"), new("Secret")]
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
            ],

            Projections = [new("Id")]
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public async Task RecursiveCteCannotUseDeniedSourceFieldsAsync()
    {
        var metadata = RestrictedUserMetadata();
        using var db = new OQueryEngine(
            new SqliteConnection("Data Source=:memory:"),
            metadata,
            new SqliteDialect());
        var query = new QueryDefinition("User")
        {

            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserTree"] = new QueryDefinition("User")
                    {

                        Projections = [new("Id"), new("Secret")]
                    },
            },
            Joins =
            [
                new JoinDefinition("UserTree", null, new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")), QueryJoinType.Left),
            ],

            Projections = [new("Id")]
        };

        Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await db.Query(query).ToListAsync());
    }

    [Test]
    public void PermissionProviderDefaultsAreExplicitlyDistinguished()
    {
        var entity = new Entity("User", [new Field("Id")]);
        var allowAll = new MetadataProvider([entity]);
        var defaultDeny = new MetadataProvider([entity], new MetadataFieldAuthorizer());

        Assert.Multiple(() =>
        {
            Assert.That(allowAll.GetField(allowAll.GetEntity("User"), "Id"), Is.Not.Null);
            Assert.That(
                () => defaultDeny.GetField(defaultDeny.GetEntity("User"), "Id"),
                Throws.TypeOf<UnauthorizedAccessException>());
        });
    }

    [Test]
    public async Task SynchronousAndAsynchronousExecutionReturnTheSameRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE User (Id INTEGER PRIMARY KEY, Name TEXT);
                INSERT INTO User (Id, Name) VALUES (1, 'Alice'), (2, 'Bob');
                """;
            command.ExecuteNonQuery();
        }

        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id"), new Field("Name")])
        ]);
        using var db = new OQueryEngine(connection, metadata, new SqliteDialect());
        var query = db.Query("User").Select("Id", "Name").OrderBy("Id");

        var synchronous = query.ToList();
        var asynchronous = await query.ToListAsync();

        Assert.That(asynchronous, Is.EqualTo(synchronous));
    }

    private static OQueryEngine CreateDatabase()
    {
        var metadata = new MetadataProvider(
        [
            new Entity(
                "User",
                [new Field("Id"), new Field("Name")],
                Relations:
                [
                    new Relation(
                        "Members",
                        "Member",
                        RelationType.Collection,
                        [new RelationItem("Id", "UserId")])
                ]),
            new Entity("Member", [new Field("Id"), new Field("UserId"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
        [
            new MetadataFieldPermission("User", ["Id", "Name"]),
            new MetadataFieldPermission("Member", ["Id"])
        ],
        accessMode: AccessMode.Allow));

        return new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
    }

    private static MetadataProvider RestrictedUserMetadata()
        => new(
        [
            new Entity("User", [new Field("Id"), new Field("Name"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
            [new MetadataFieldPermission("User", ["Id", "Name"])],
            accessMode: AccessMode.Allow));

    private static MetadataProvider RestrictedRelationMetadata()
        => new(
        [
            new Entity("User", [new Field("Id"), new Field("Secret")]),
            new Entity("Member", [new Field("Id"), new Field("Secret")])
        ],
        new MetadataFieldAuthorizer(
        [
            new MetadataFieldPermission("User", ["Id"]),
            new MetadataFieldPermission("Member", ["Id"])
        ],
        accessMode: AccessMode.Allow));
}
