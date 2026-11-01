namespace OQuery.Test.Tests;


[TestFixture]
public class _03_Filter_OperatorTest : TestBase
{
    [Test]
    public void _21_Alias()
    {
        var query = _db.Query("User")
            .Select(e => new Dictionary<string, object?>
            {
                { "Name", e["Name"] },
                { "Manager-Name", e["Manager.Name"] }
            })
            .Where(e => e["Manager-Name"] == "Alice");

        Assert.That(query.Count(), Is.EqualTo(2));

    }

    [Test]
    public void _31_Logical_And()
    {
        var query = _db.Query("User")
            .Where(e => e["Age"] > 32 && e["IsMale"] == true);
        Assert.That(query.Count(), Is.EqualTo(1));
    }

    [Test]
    public void _31_Logical_And_UseWhere()
    {
        var query = _db.Query("User")
            .Where(e =>e.Query("Members").Count() >= 1)
            .Where(e => e["Age"] > 32 && e["IsMale"] == true);
        Assert.That(query.Count(), Is.EqualTo(1));
    }

    [Test]
    public void _31_Property_Equality()
    {
        var query = _db.Query("User")
            .Where(e => e.Query("Members").Any(x => x["IsMale"] == e["IsMale"]));
        Assert.That(query.Count(), Is.EqualTo(2));
    }


    [Test]
    public void _41_ComplexFilters()
    {
        var query = _db.Query("User")
            .Where(e => e.Query("Members").Any(x => x["IsMale"] == e["IsMale"] && x.Query("Members").Count() >= 1));
        Assert.That(query.Count(), Is.EqualTo(1));
    }
}
