using Microsoft.Data.Sqlite;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.Test.Tests;

[TestFixture]
public sealed class _55_MetadataAuthorizationPathTests
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
        new PermissionManagement(
            [new MetadataPermission("User", ["Id"])],
            IsDenied: false));
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
    public void CteProjectionCannotBypassFieldAuthorization()
    {
        var metadata = new MetadataProvider(
        [
            new Entity("User", [new Field("Id"), new Field("Secret")])
        ],
        new PermissionManagement(
            [new MetadataPermission("User", ["Id"])],
            IsDenied: false));
        using var db = new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "UserContext",
                    new QueryDefinition
                    {
                        Entity = "User",
                        Projection = [new("Secret")]
                    },
                    Condition: null)
            ],
            Projection = [new("Id")]
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
        var query = new QueryDefinition
        {
            Entity = "User",
            Projection = [new("Id"), new("COUNT(Id) | Total")],
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
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts =
            [
                new QueryContextDefintion(
                    "MemberContext",
                    new QueryDefinition
                    {
                        Entity = "Member",
                        Projection = [new("Id")]
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("Secret"),
                        BinaryOperator.Equals,
                        new FieldDefinition("Id")))
            ],
            Projection = [new("Id")]
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
        new PermissionManagement(
        [
            new MetadataPermission("User", ["Id"]),
            new MetadataPermission("Member", ["Id"]),
            new MetadataPermission("Tag", ["Id", "MemberId"])
        ],
        IsDenied: false));
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
        var query = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("COUNT(*) | Total")
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
                        Projection = [new("Id"), new("Secret")]
                    },
                    new ConditionStatementDefinition(
                        new FieldDefinition("Id"),
                        BinaryOperator.Equals,
                        new FieldDefinition("UserTree.Id")))
                    {
                        Recursive = true
                    }
            ],
            Projection = [new("Id")]
        };

        Assert.That(
            () => db.Query(query).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public void PermissionProviderDefaultsAreExplicitlyDistinguished()
    {
        var entity = new Entity("User", [new Field("Id")]);
        var allowAll = new MetadataProvider([entity]);
        var defaultDeny = new MetadataProvider([entity], new PermissionManagement());

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
        new PermissionManagement(
        [
            new MetadataPermission("User", ["Id", "Name"]),
            new MetadataPermission("Member", ["Id"])
        ],
        IsDenied: false));

        return new OQueryEngine(new SqliteConnection(), metadata, new SqliteDialect());
    }

    private static MetadataProvider RestrictedUserMetadata()
        => new(
        [
            new Entity("User", [new Field("Id"), new Field("Name"), new Field("Secret")])
        ],
        new PermissionManagement(
            [new MetadataPermission("User", ["Id", "Name"])],
            IsDenied: false));

    private static MetadataProvider RestrictedRelationMetadata()
        => new(
        [
            new Entity("User", [new Field("Id"), new Field("Secret")]),
            new Entity("Member", [new Field("Id"), new Field("Secret")])
        ],
        new PermissionManagement(
        [
            new MetadataPermission("User", ["Id"]),
            new MetadataPermission("Member", ["Id"])
        ],
        IsDenied: false));
}
