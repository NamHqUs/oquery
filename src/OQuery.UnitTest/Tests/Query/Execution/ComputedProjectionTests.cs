using OQuery.UnitTest;

namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
public sealed class ComputedProjectionTests : TestBase
{
    [Test]
    public void CollectionCountCanBeUsedInComputedProjection()
    {
        var query = _db.Query("User")
            .Select(e => new Dictionary<string, object?>
            {
                {"Computed", e.Query("Members").Count("Id") + 1 }
            });

        var data = query.ToList();

        Assert.That(
            data.Select(record => Convert.ToInt32(record["Computed"])),
            Is.EqualTo(new[] { 3, 2, 1, 3, 1, 1 }));
    }
}
