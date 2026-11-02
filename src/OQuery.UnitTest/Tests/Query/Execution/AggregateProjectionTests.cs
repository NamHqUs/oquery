namespace OQuery.Test.Tests;

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
    public void CollectionField_Projection_Count_Nested()
    {
        var query = _db.Query("User").Select("Name", "Members.Members.Count(Id)");
        var data = query.ToList();

        Assert.That(data, Is.Not.Empty);
        Assert.That(data.All(record => record.ContainsKey("Members.Members.Count(Id)")), Is.True);
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
