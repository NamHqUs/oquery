namespace OQuery.Test.Tests;

[TestFixture]
public sealed class PagingExecutionTests : TestBase
{
    [Test]
    public void TakeLimitsUnprojectedResults()
    {
        var data = _db.Query("User").Take(3);

        Assert.That(data, Has.Count.EqualTo(3));
    }

    [Test]
    public void TakePreservesRequestedProjection()
    {
        var data = _db.Query("User")
            .Select("Id", "Age")
            .Take(4);

        Assert.Multiple(() =>
        {
            Assert.That(data, Has.Count.EqualTo(4));
            Assert.That(data[0], Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void TakeAppliesAfterFiltering()
    {
        var data = _db.Query("User")
            .Where(e => e["Age"] > 30)
            .Select("Id", "Age")
            .Take(2);

        Assert.Multiple(() =>
        {
            Assert.That(data, Has.Count.EqualTo(2));
            Assert.That(data.All(row => Convert.ToInt32(row["Age"]) > 30), Is.True);
        });
    }
}
