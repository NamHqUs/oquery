namespace OQuery.Test.Tests;

[TestFixture]
public class ComputedTest : TestBase
{
    [Test]
    public void SimpleProjection()
    {
        var query = _db.Query("User")
            .Select(e => new Dictionary<string, object?>
            {
                {"Computed", e.Query("Members").Count("Id") + 1 }
            });

        var queryString = query.ToString();
        var data = query.ToList();
    }
}
