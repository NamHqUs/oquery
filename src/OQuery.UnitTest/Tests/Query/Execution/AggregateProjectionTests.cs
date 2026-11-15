using OQuery.UnitTest;
using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
public sealed class AggregateProjectionTests : TestBase
{
    [Test]
    public void CollectionField_Projection_Sum()
    {
        var query = _db.Query("User").Select("Name", "Members.Sum(Age)");
        var data = query.ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToInt32(e["Members.Sum(Age)"] ?? 0));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, int>
        {
            ["Alice"] = 59,
            ["Bob"] = 35,
            ["Charlie"] = 0,
            ["David"] = 49,
            ["Eve"] = 0,
            ["Frank"] = 0,
        }));
    }

    [Test]
    public void CollectionField_Projection_Sum_Nested()
    {
        var query = _db.Query("User").Select("Name", "Members.Sum(Age)", "Members.Members.Sum(Age)");
        var data = query.ToList();

        Assert.That(data, Is.Not.Empty);
        Assert.That(data.All(record => record.ContainsKey("Members.Members.Sum(Age)")), Is.True);
    }

    [Test]
    public void CollectionField_Projection_Count()
    {
        var query = _db.Query("User").Select("Name", "Members.Count(Id)");
        var data = query.ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToInt32(e["Members.Count(Id)"]));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, int>
        {
            ["Alice"] = 2,
            ["Bob"] = 1,
            ["Charlie"] = 0,
            ["David"] = 2,
            ["Eve"] = 0,
            ["Frank"] = 0,
        }));
    }

    [Test]
    public void CollectionField_Projection_CountWithoutAnArgument()
    {
        var query = _db.Query("User").Select("Name", "Members.Count()");
        var data = query.ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToInt32(e["Members.Count()"]));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, int>
        {
            ["Alice"] = 2,
            ["Bob"] = 1,
            ["Charlie"] = 0,
            ["David"] = 2,
            ["Eve"] = 0,
            ["Frank"] = 0,
        }));
    }

    [Test]
    public void CollectionField_Projection_CountIf()
    {
        var query = _db.Query("User")
            .Select(
                "Id",
                "Name",
                "Members.Name",
                "Members.CountIf(Name like <Bob>)",
                "Members.CountIf(Name like <David>)");
        var data = query.ToList();

        var actualValues = data
            .GroupBy(record => (string)record["Name"]!)
            .ToDictionary(
                group => group.Key,
                group => (
                    Bob: Convert.ToInt32(group.First()["Members.CountIf(Name like <Bob>)"]),
                    David: Convert.ToInt32(group.First()["Members.CountIf(Name like <David>)"])));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, (int Bob, int David)>
        {
            ["Alice"] = (1, 0),
            ["Bob"] = (0, 1),
            ["Charlie"] = (0, 0),
            ["David"] = (0, 0),
            ["Eve"] = (0, 0),
            ["Frank"] = (0, 0),
        }));
    }

    [Test]
    public void CollectionField_Projection_CountIf_WithExpressionMatchesOneArgumentForm()
    {
        var data = _db.Query("User")
            .Select("Name", "Members.CountIf(Name like <Bob>)", "Members.CountIf(Id, Name like <Bob>)")
            .ToList();

        Assert.That(data.Select(record => (
            Name: (string)record["Name"]!,
            OneArgument: Convert.ToInt32(record["Members.CountIf(Name like <Bob>)"]),
            TwoArguments: Convert.ToInt32(record["Members.CountIf(Id, Name like <Bob>)"])))
            .All(row => row.OneArgument == row.TwoArguments), Is.True);
    }

    [Test]
    public void CollectionField_Projection_ConditionalAggregates()
    {
        var query = _db.Query("User")
            .Select(
                "Name",
                "Members.CountIf(Name like <Bob>)",
                "Members.SumIf(Age, Name like <Bob>)",
                "Members.AvgIf(Age, Name like <Bob>)",
                "Members.MinIf(Age, Name like <Bob>)",
                "Members.MaxIf(Age, Name like <Bob>)");
        var data = query.ToList().ToDictionary(
            record => (string)record["Name"]!,
            record => new
            {
                Count = Convert.ToInt32(record["Members.CountIf(Name like <Bob>)"]),
                Sum = Convert.ToInt32(record["Members.SumIf(Age, Name like <Bob>)"]),
                Average = Convert.ToInt32(record["Members.AvgIf(Age, Name like <Bob>)"]),
                Minimum = Convert.ToInt32(record["Members.MinIf(Age, Name like <Bob>)"]),
                Maximum = Convert.ToInt32(record["Members.MaxIf(Age, Name like <Bob>)"])
            });

        Assert.That(data["Alice"], Is.EqualTo(new
        {
            Count = 1,
            Sum = 31,
            Average = 31,
            Minimum = 31,
            Maximum = 31
        }));
        Assert.That(data["Bob"], Is.EqualTo(new
        {
            Count = 0,
            Sum = 0,
            Average = 0,
            Minimum = 0,
            Maximum = 0
        }));
    }

    [Test]
    public void CollectionField_Projection_Count_Nested()
    {
        var query = _db.Query("User").Select("Name", "Members.Members.Count(Id)");
        var data = query.ToList();

        Assert.That(data, Is.Not.Empty);
        Assert.That(data.All(record => record.ContainsKey("Members.Members.Count(Id)")), Is.True);
    }

    [Test]
    public void CollectionAggregate_IsIndependentOfCollectionDetailProjection()
    {
        var countOnly = _db.Query("User")
            .Select("Id", "Members.Count(Id)")
            .ToList()
            .ToDictionary(
                record => Convert.ToInt32(record["Id"]),
                record => record["Members.Count(Id)"]);

        var withMemberName = _db.Query("User")
            .Select("Id", "Members.Name", "Members.Count(Id)")
            .ToList();

        Assert.That(withMemberName, Is.Not.Empty);
        Assert.That(
            withMemberName.All(record =>
                Equals(
                    record["Members.Count(Id)"],
                    countOnly[Convert.ToInt32(record["Id"])])),
            Is.True);
    }

    [Test]
    public void CollectionField_Projection_SumOfNestedCountIf_MatchesIndependentCount()
    {
        var users = _db.Query("User").Select("Id", "Name", "ManagerId", "Age").ToList();
        var expected = users.ToDictionary(
            manager => (string)manager["Name"]!,
            manager => users
                .Where(member => IsManagedBy(member, manager))
                .Sum(member => users.Count(report =>
                    IsManagedBy(report, member) && Convert.ToInt32(report["Age"]) > 30)));

        var actual = _db.Query("User")
            .Select("Name", "Members.Sum(Members.CountIf(Age > <30>))")
            .ToList()
            .ToDictionary(
                e => (string)e["Name"]!,
                e => Convert.ToInt32(e["Members.Sum(Members.CountIf(Age > <30>))"] ?? 0));

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CollectionField_Projection_SumOfNestedCountIf_RendersGroupedJoinWithoutCte()
    {
        var sql = _db.Query("User")
            .Select("Name", "Members.Sum(Members.CountIf(Age > <30>))")
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("COUNT (CASE WHEN"));
        Assert.That(sql, Does.Contain("GROUP BY"));
        Assert.That(sql, Does.Not.Contain("WITH"));
    }

    private static bool IsManagedBy(IDictionary<string, object?> member, IDictionary<string, object?> manager)
        => member["ManagerId"] is not null &&
            Convert.ToInt32(member["ManagerId"]) == Convert.ToInt32(manager["Id"]);

    [Test]
    public void CollectionField_Projection_SumOfNestedSum_ReturnsTotalsOfMemberTotals()
    {
        var data = _db.Query("User")
            .Select("Name", "Members.Sum(Members.Sum(Age))")
            .ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToInt32(e["Members.Sum(Members.Sum(Age))"] ?? 0));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, int>
        {
            ["Alice"] = 35,
            ["Bob"] = 49,
            ["Charlie"] = 0,
            ["David"] = 0,
            ["Eve"] = 0,
            ["Frank"] = 0,
        }));
    }

    [Test]
    public void CollectionField_Projection_SumOfNestedCount_ReturnsTotalsOfMemberCounts()
    {
        var data = _db.Query("User")
            .Select("Name", "Members.Sum(Members.Count(Id))")
            .ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToInt32(e["Members.Sum(Members.Count(Id))"] ?? 0));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, int>
        {
            ["Alice"] = 1,
            ["Bob"] = 2,
            ["Charlie"] = 0,
            ["David"] = 0,
            ["Eve"] = 0,
            ["Frank"] = 0,
        }));
    }

    [Test]
    public void CollectionField_Projection_SumOfNestedCount_RendersGroupedJoinsWithoutCte()
    {
        var sql = _db.Query("User")
            .Select("Name", "Members.Sum(Members.Count(Id))")
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("SUM (COALESCE (t4.\"a0\", 0))"));
        Assert.That(sql, Does.Contain("COUNT (t3.\"Id\")"));
        Assert.That(sql, Does.Not.Contain("WITH"));
    }

    [Test]
    public void CollectionField_Projection_ChainsAggregate()
    {
        var query = _db.Query("User")
            .Select(
                "Name",
                "Members.Sum(Age).Avg().Sum()");

        var data = query.ToList();

        Assert.That(data, Is.Not.Empty);
    }

    [Test]
    public void CollectionField_Projection_AggregateCanUseFluentFunction()
    {
        var query = _db.QueryOqr("""
            User:
                Name,
                Members.Sum(Age).Round(<1>) RoundedAge
            """);

        var data = query.ToList().ToDictionary(
            record => (string)record["Name"]!,
            record => Convert.ToDecimal(record["RoundedAge"]));

        Assert.That(data, Is.EqualTo(new Dictionary<string, decimal>
        {
            ["Alice"] = 59m,
            ["Bob"] = 35m,
            ["Charlie"] = 0m,
            ["David"] = 49m,
            ["Eve"] = 0m,
            ["Frank"] = 0m,
        }));
    }

    [Test]
    public void CollectionField_Projection_ChainedAggregate_DoesNotCopyHiddenPipelineProjection()
    {
        var sql = _db.Query("User")
            .Select("Name", "Members.Sum(Id).Avg().Max()")
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain(
            "MAX (q0.\"__oq_pipeline_3\") OVER () \"Members.Sum(Id).Avg().Max()\""));
        Assert.That(sql, Does.Not.Contain(
            "q0.\"__oq_pipeline_3\" \"__oq_pipeline_3\""));
        Assert.That(sql, Does.Not.Contain("AS q2"));
    }

    [Test]
    public void CollectionField_Projection_ChainedAggregate_FilterDoesNotReferenceRemovedPipelineProjection()
    {
        var query = _db.QueryOqr("""
            User:
                Name,
                Members.Sum(Id).Avg().Max() Temp
            WHERE Temp > <0>
            """);

        var data = query.ToList();
        var sql = query.ToSql().CommandText;

        Assert.That(data, Is.Not.Null);
        Assert.That(sql, Does.Contain("WHERE"));
        Assert.That(sql, Does.Not.Contain(
            "q1.\"__oq_pipeline_3\" \"__oq_pipeline_3\""));
    }

    [Test]
    public void OQueryDefinition_ResolvesFluentAggregateProjectionAliases()
    {
        var query = _db.QueryOqr("""
            Report is Products:
                Name,
                Revenue.Avg().Sum() Average,
                OrderItems.Sum(Quantity * UnitPrice) Revenue
            WHERE Revenue > Average
            """);

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Contain("Average"));
        Assert.That(sql, Does.Contain("Revenue"));
    }

    [Test]
    public void OQuery_ContextCollection_ResolvesFluentAggregateWithAlias()
    {
        var query = _db.QueryOqr("""
            UserRows is User:
                Id,
                Name,
                Age,
                ManagerId
            User:
              Join UserRows r On Id = r.ManagerId
              Select Id,
                Name Manager,
                r.Name Members,
                r.Count(Id) MemberCount
            """);

        var expected = _db.Query("User")
            .Select("Id", "Members.Count(Id)")
            .ToList()
            .ToDictionary(
                record => Convert.ToInt32(record["Id"]),
                record => record["Members.Count(Id)"]);
        var data = query.ToList();

        Assert.That(
            data.All(record =>
                Equals(
                    record["MemberCount"],
                    expected[Convert.ToInt32(record["Id"])])),
            Is.True);
    }

    [Test]
    public void OQuery_ContextCollection_ResolvesMultipleFluentCountIfAggregates()
    {
        var query = _db.QueryOqr("""
            UserRows is User:
                Id,
                Name,
                Age,
                ManagerId
            User:
              Join UserRows r On Id = r.ManagerId
              Select Id,
                Name,
                r.CountIf(Name like <Bob>) BobMembers,
                r.SumIf(Age, Name like <Bob>) BobAgeTotal,
                r.AvgIf(Age, Name like <Bob>) BobAgeAverage,
                r.MinIf(Age, Name like <Bob>) BobYoungest,
                r.MaxIf(Age, Name like <Bob>) BobOldest
            """);

        var data = query.ToList()
            .GroupBy(record => (string)record["Name"]!)
            .ToDictionary(
                group => group.Key,
                group => (
                    Count: Convert.ToInt32(group.First()["BobMembers"]),
                    Sum: Convert.ToInt32(group.First()["BobAgeTotal"]),
                    Average: Convert.ToInt32(group.First()["BobAgeAverage"]),
                    Minimum: Convert.ToInt32(group.First()["BobYoungest"]),
                    Maximum: Convert.ToInt32(group.First()["BobOldest"])));

        Assert.That(data["Alice"], Is.EqualTo((1, 31, 31, 31, 31)));
        Assert.That(data["Bob"], Is.EqualTo((0, 0, 0, 0, 0)));
        Assert.That(data["David"], Is.EqualTo((0, 0, 0, 0, 0)));
    }

    [Test]
    public void OQuery_ContextCollection_UsesContextNameWhenAliasIsOmitted()
    {
        var query = _db.QueryOqr("""
            OrderCategory is OrderItems:
                Product.Category Category,
                SUM(Quantity * UnitPrice) NetRevenue
            Report is OrderCategory:
              Join OrderCategory
              Select Category,
                OrderCategory.Sum(NetRevenue) TotalRevenue
            """);

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Contain("SUM"));
    }

    [Test]
    public void OQuery_ContextCollection_AggregateCanUseFluentFunction()
    {
        var query = _db.QueryOqr("""
            OrderCategory is OrderItems:
                Product.Category Category,
                SUM(Quantity * UnitPrice) NetRevenue
            Report is OrderCategory:
              Join OrderCategory
              Select Category,
                OrderCategory.Sum(NetRevenue).Round(<2>) RoundedRevenue
            """);

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Contain("ROUND"));
    }

    [Test]
    public void OQuery_EntityCollection_ResolvesFluentAggregateWithAlias()
    {
        var query = _db.QueryOqr("""
            User:
              Join User r On Id = r.ManagerId
              Select Id,
                Name Manager,
                r.Name Members,
                r.Count(Id) MemberCount,
                r.Sum(r.Id) MemberSum,
                r.Avg(r.Id) MemberAverage,
                r.Min(r.Id) MemberMinimum,
                r.Max(r.Id) MemberMaximum
            """);

        var expected = _db.Query("User")
            .Select("Id", "Members.Count(Id)")
            .ToList()
            .ToDictionary(
                record => Convert.ToInt32(record["Id"]),
                record => record["Members.Count(Id)"]);
        var data = query.ToList();

        Assert.That(
            data.All(record =>
                Equals(
                    record["MemberCount"],
                    expected[Convert.ToInt32(record["Id"])])),
            Is.True);
    }

    [Test]
    public void OQuery_EntityCollection_ResolvesMultipleFluentCountIfAggregates()
    {
        var query = _db.QueryOqr("""
            User:
              Join User r On Id = r.ManagerId
              Select Id,
                Name,
                r.CountIf(Name like <Bob>) BobMembers,
                r.SumIf(Age, Name like <Bob>) BobAgeTotal,
                r.AvgIf(Age, Name like <Bob>) BobAgeAverage,
                r.MinIf(Age, Name like <Bob>) BobYoungest,
                r.MaxIf(Age, Name like <Bob>) BobOldest
            """);

        var data = query.ToList()
            .GroupBy(record => (string)record["Name"]!)
            .ToDictionary(
                group => group.Key,
                group => (
                    Count: Convert.ToInt32(group.First()["BobMembers"]),
                    Sum: Convert.ToInt32(group.First()["BobAgeTotal"]),
                    Average: Convert.ToInt32(group.First()["BobAgeAverage"]),
                    Minimum: Convert.ToInt32(group.First()["BobYoungest"]),
                    Maximum: Convert.ToInt32(group.First()["BobOldest"])));

        Assert.That(data["Alice"], Is.EqualTo((1, 31, 31, 31, 31)));
        Assert.That(data["Bob"], Is.EqualTo((0, 0, 0, 0, 0)));
        Assert.That(data["David"], Is.EqualTo((0, 0, 0, 0, 0)));
    }

    [Test]
    public void OQuery_EntityCollection_UsesEntityNameWhenAliasIsOmitted()
    {
        var query = _db.QueryOqr("""
            User:
              Join User On Id = User.ManagerId
              Select Id,
                Name,
                User.Count(User.Id) MemberCount
            """);

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Contain("COUNT"));
    }

    [Test]
    public void OQuery_CollectionField_Projection_Count()
    {
        var definition = QueryDefinitionOQueryParser.Parse(
            "User: Id, Name, Members.Count(Id) TotalMembers");
        var data = _db.Query(definition).ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToInt32(e["TotalMembers"]));

        Assert.That(actualValues, Is.EqualTo(new Dictionary<string, int>
        {
            ["Alice"] = 2,
            ["Bob"] = 1,
            ["Charlie"] = 0,
            ["David"] = 2,
            ["Eve"] = 0,
            ["Frank"] = 0,
        }));
    }

    [Test]
    public void OQuery_GroupedAggregateExpression_WithFunctionReceiver_Rounds()
    {
        var definition = QueryDefinitionOQueryParser.Parse(
            "User: Name, (CountIf(Age >= 30) * 100.0 / Count(Id)).Round(2) OldPercent");
        var data = _db.Query(definition).ToList();

        var actualValues = data.ToDictionary(
            e => (string)e["Name"]!,
            e => Convert.ToDecimal(e["OldPercent"]));

        Assert.That(actualValues["Alice"], Is.EqualTo(100m));
        Assert.That(actualValues["Charlie"], Is.EqualTo(0m));
    }

    //[Test]
    //public void Count()
    //{
    //    var count = _db.Query("user")
    //        .Select("Id", "ManagerId", "SchoolId", "Name", "Age", "Birthday", "IsMale")
    //        .Count();
    //    Assert.AreEqual(count, 6);

    //    count = _db.Query("user").Count();
    //    Assert.AreEqual(count, 6);

    //    count = (int)_db.Query("Student").Aggregate("TotalMembers");
    //    Assert.AreEqual(count, 6);

    //    var val1 = _db.Query("Student").Aggregate("CountAgeWithNam");
    //    var val2 = _db.Query("Student").Where(e => e["Name"] == "Nam").Count();
    //    Assert.AreEqual(val1, val2);
    //}

    //[Test]
    //public void ProjectValue()
    //{
    //    var val1 = _db.Query("Student").Aggregate("TotalMembers");
    //    var val2 = _db.Query("Student").Count();
    //    Assert.AreEqual(val1, 6);
    //    Assert.AreEqual(val1, val2);

    //    var val = _db.Query("Student").Aggregate("TotalAges");
    //    var ages = _db.Query("Student").Select("Age").ToList().Sum(e => (int)e["Age"]!);
    //    Assert.AreEqual(val, ages);
    //}

    //[Test]
    //public void ProjectByText()
    //{
    //    var query = _db.Query("User")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "Name", "Name" },
    //                { "Total0", "Members.TotalMembers"},
    //                { "Total1", e["Members.TotalMembers"]},
    //                { "Total2", e.Query("Members").Count()},
    //                { "Total3", e.Query("Members").Aggregate("TotalMembers")}
    //        });

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    foreach (var e in data)
    //    {
    //        Assert.AreEqual(e["Total0"], e["Total1"]);
    //        Assert.AreEqual(e["Total1"], e["Total2"]);
    //        Assert.AreEqual(e["Total2"], e["Total3"]);
    //    }
    //}

    //[Test]
    //public void ProjectManyAndMany()
    //{
    //    //var total = _db.Query("User").Aggregate("Members.Members.TotalMembers");
    //    //Assert.AreEqual(total, 3);

    //    var query = _db.Query("User")
    //        .Select(e => new Dictionary<string, object?> {
    //                {"Name", "Name" },
    //                {"Name1", "Members.Name" },
    //                {"Name1.Age", "Members.Age" },
    //                {"Total0", e["Members.TotalAges"]},
    //                {"Total1", e.Query("Members").Aggregate("TotalAges")},
    //        });

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    Assert.AreEqual(data.Count, 5);
    //    Assert.AreEqual(data[0]["Total0"], 80);
    //    Assert.AreEqual(data[2]["Total0"], 65);
    //    Assert.AreEqual(data[4]["Total0"], 30);
    //    foreach (var e in data)
    //        Assert.AreEqual(e["Total0"], e["Total1"]);

    //    query = _db.Query("User")
    //        .Select(e => new Dictionary<string, object?> {
    //                {"Name", "Name" },
    //                {"Name1", "Members.Name" },
    //                {"Name1.Age", "Members.Age" },
    //                {"Name2", "Members.Members.Name" },
    //                {"Name2.Age", "Members.Members.Age" },
    //                {"Total0", e["Members.Members.TotalAges"]},
    //                {"Total1", e.Query("Members.Members").Aggregate("TotalAges")},
    //        });

    //    queryString = query.ToString();
    //    data = query.ToList();

    //    Assert.AreEqual(data.Count, 3);
    //    Assert.AreEqual(data[0]["Total0"], 95);
    //    Assert.AreEqual(data[0]["Total1"], 65);
    //    Assert.AreEqual(data[2]["Total0"], 95);
    //    Assert.AreEqual(data[2]["Total1"], 30);
    //}

    //[Test]

    //public void ProjectWithSubFilter1()
    //{
    //    var query = _db.Query("School")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "Name", "Name" },
    //                { "Total1", "Students.CountAgeWithNam"},
    //                { "Total2", e["Students.CountAgeWithNam"]},
    //                { "Total3", e.Query("Students").Where(x => x["Name"] == "Nam").Count()},
    //        });

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    foreach (var e in data)
    //    {
    //        Assert.AreEqual(e["Total1"], e["Total2"]);
    //        Assert.AreEqual(e["Total2"], e["Total3"]);
    //    }
    //}

    //[Test]
    //public void ProjectWithSubFilter2()
    //{
    //    var query = _db.Query("User")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "TotalMembers", e.Query("Members").Where(x => x["Name"] == e["Name"]).Count()},
    //        });

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    Assert.AreEqual(data.Count, 6);
    //    Assert.AreEqual(data.Where(e => (int)e["TotalMembers"]! > 0).Count(), 1);

    //    query = _db.Query("School")
    //       .Select(e => new Dictionary<string, object?>{
    //                { "School Name", "Name" },
    //                { "Students", e.Query("Students").Where(x => x["Name"] == "Nam").Count()},
    //       })
    //       .Where(e => e["students"] > 0);

    //    queryString = query.ToString();
    //    data = query.ToList();
    //}

    //[Test]
    //public void ComplexProjection2()
    //{
    //    var query = _db.Query("School")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "Name", "Name" },
    //                { "TotalMembers", e.Query("Students").Where(x => x["Age"] > 30).Aggregate("TotalMembers")},
    //                { "TotalAges", e.Query("Students").Where(x => x["Age"] > 20).Aggregate("TotalAges")},
    //        });

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    Assert.AreEqual(data.Sum(e => (int)(e["TotalMembers"] ?? 0)), 3);
    //    Assert.AreEqual(data.Sum(e => (int)(e["TotalAges"] ?? 0)), 175);
    //}

    //[Test]
    //public void SimpleFilter()
    //{
    //    var count = _db.Query("user").Where(e => e["Name"] == "Nam").Count();
    //    Assert.AreEqual(count, 2);

    //    count = (int)_db.Query("Student").Where(e => e["Name"] == "Nam").Aggregate("TotalMembers");
    //    Assert.AreEqual(count, 2);
    //}

    //[Test]
    //public void ComplexFilter1()
    //{
    //    var query = _db.Query("User")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "Name", "Name" },
    //                { "TotalMembers", e.Query("Members").Where(x => x["Age"] < 40).Count()},
    //        })
    //        .Where(e => e["TotalMembers"] > 0);

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    Assert.AreEqual(data.Count, 3);
    //}

    //[Test]
    //public void ComplexFilter2()
    //{
    //    var query = _db.Query("School")
    //       .Select(e => new Dictionary<string, object?>{
    //                { "Name", "Name" },
    //                { "Total1", e.Query("Students").Where(x => x["Age"] > 30).Aggregate("TotalMembers")},
    //                { "Total2", e.Query("Students").Where(x => x["Age"] > 20).Aggregate("TotalAges")},
    //       })
    //       .Where(e => e["Total1"] >= 2 && e["Total2"] >= 100);

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    Assert.AreEqual(data.Count, 1);
    //}

    //[Test]
    //public void ComplexFilter3()
    //{
    //    var query = _db.Query("School")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "Name", "Name" },
    //        })
    //        .Where(e => e.Query("Students").Where(x => x["Age"] < 40).Aggregate("TotalMembers") > 0);

    //    var queryString = query.ToString();
    //    var data = query.ToList();

    //    Assert.AreEqual(data.Count, 2);
    //}

    //[Test]
    //public void RawAggregateFieldWithFilter()
    //{
    //    var data1 = _db.Query("School")
    //        .Select(e => new Dictionary<string, object?>{
    //                { "Total", e["Students.CountAgeWithNam"]},
    //        })
    //        .Where(e => e["Total"] > 0)
    //        .ToList();

    //    var data2 = _db.Query("School")
    //        .Select(e => new Dictionary<string, object?>
    //        {
    //                {"Total", e.Query("Students").Where(x => x["Name"] == "Nam").Count()}
    //        })
    //        .Where(e => e["Total"] > 0)
    //        .ToList();

    //    for (var i = 0; i < data1.Count; i++)
    //        Assert.AreEqual(data1[i]["Total"], data2[i]["Total"]);
    //}
}
