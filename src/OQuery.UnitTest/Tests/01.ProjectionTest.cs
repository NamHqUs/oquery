namespace OQuery.Test.Tests;

[TestFixture]
public class _01_ProjectionTest : TestBase
{
    [Test]
    public void _01_Field_Primitive()
    {
        string[] fields = ["Id", "Name", "Age", "Birthday", "IsMale"];
        var query = _db.Query("User").Select(fields);
        var data = query.ToList();

        Assert.That(data.Count, Is.EqualTo(6));
        Assert.That(data[0].Keys.ToArray(), Is.EquivalentTo(fields));
    }

    [Test]
    public void _02_Field_Lookup()
    {
        var query = _db.Query("User").Select("Id", "Name", "Manager.Name");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - NULL", 
            "Bob - Alice", 
            "Charlie - Alice", 
            "David - Bob", 
            "Eve - David", 
            "Frank - David"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager.Name"]??"NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _02_Field_Lookup_Lookup()
    {
        var query = _db.Query("User").Select("Id", "Name", "Manager.Name", "Manager.Manager.Name");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - NULL - NULL",
            "Bob - Alice - NULL",
            "Charlie - Alice - NULL",
            "David - Bob - Alice",
            "Eve - David - Bob",
            "Frank - David - Bob"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager.Name"] ?? "NULL"} - {e["Manager.Manager.Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _02_Field_NestedDataRecord()
    {
        var query = _db.Query("User").Select(new Dictionary<string, object?>
        {
            { "Manager", new Dictionary<string, object?> { { "Name", "Manager.Name" } } }
        });

        var data = query.ToList();

        Assert.That(data.Select(record => record["Manager.Name"]), Is.EquivalentTo(
            new[] { null, "Alice", "Alice", "Bob", "David", "David" }));
    }

    [Test]
    public void _02_Field_Lookup_Collection()
    {
        var query = _db.Query("User").Select("Id", "Name", "Manager.Name", "Manager.Members.Name");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - NULL - NULL",
            "Bob - Alice - Bob",
            "Bob - Alice - Charlie",
            "Charlie - Alice - Bob",
            "Charlie - Alice - Charlie",
            "David - Bob - David",
            "Eve - David - Eve",
            "Eve - David - Frank",
            "Frank - David - Eve",
            "Frank - David - Frank"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager.Name"] ?? "NULL"} - {e["Manager.Members.Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _03_Field_Collection()
    {
        var query = _db.Query("User").Select("Name", "Members.Name");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - Bob", 
            "Alice - Charlie", 
            "Bob - David", 
            "Charlie - NULL", 
            "David - Eve", 
            "David - Frank", 
            "Eve - NULL", 
            "Frank - NULL"];
        var dataDict = data.ToDictionary(e => $"{e["Name"]} - {e["Members.Name"]??"NULL"}");
        foreach(var ele in expectedValues)
            Assert.That(dataDict.ContainsKey(ele), Is.True);
    }

    [Test]
    public void _03_Field_Collection_Lookup()
    {
        var query = _db.Query("User").Select("Name", "Members.Name", "Members.Manager.Name");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - Bob - Alice",
            "Alice - Charlie - Alice",
            "Bob - David - Bob",
            "Charlie - NULL - NULL",
            "David - Eve - David",
            "David - Frank - David",
            "Eve - NULL - NULL",
            "Frank - NULL - NULL"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Members.Name"] ?? "NULL"} - {e["Members.Manager.Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _03_Field_Collection_Collection()
    {
        var query = _db.Query("User").Select("Name", "Members.Name", "Members.Members.Name");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - Bob - David",
            "Alice - Charlie - NULL",
            "Bob - David - Eve",
            "Bob - David - Frank",
            "Charlie - NULL - NULL",
            "David - Eve - NULL",
            "David - Frank - NULL",
            "Eve - NULL - NULL",
            "Frank - NULL - NULL"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Members.Name"] ?? "NULL"} - {e["Members.Members.Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _04_Field_Collection_Aggregate_Function()
    {
        var query = _db.Query("User").Select("Id", "Name", "Members.COUNT(Id)");
        var data = query.ToList();

        string[] expectedValues = [
            "Alice - 2",
            "Bob - 1",
            "Charlie - 0",
            "David - 2",
            "Eve - 0",
            "Frank - 0"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Members.COUNT(Id)"]}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _10_Alias()
    {
        Dictionary<string, object?> projection = new()
        {
            { "Id", "Id" },
            { "Name", "Name" },
            { "Manager-Name", "Manager.Name" },
            { "Manager-Manager-Name", "Manager.Manager.Name" },
        };
        var query = _db.Query("User").Select(projection);
        var data = query.ToList();

        string[] expectedValues = [
           "Alice - NULL - NULL",
            "Bob - Alice - NULL",
            "Charlie - Alice - NULL",
            "David - Bob - Alice",
            "Eve - David - Bob",
            "Frank - David - Bob"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager-Name"] ?? "NULL"} - {e["Manager-Manager-Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _10_Alias_Compute()
    {
        var query = _db.Query("User")
            .Select(e => new Dictionary<string, object?>
                {
                    { "Id", "Id" },
                    { "Manager-Name", e["Name"] + "." + e["Manager.Name"] },
                });
        var data = query.ToList();

        string[] expectedValues = [
            "NULL",
            "Bob.Alice",
            "Charlie.Alice",
            "David.Bob",
            "Eve.David",
            "Frank.David"];
        var actualValues = data.Select(e => e["Manager-Name"]??"NULL").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _20_Aggregate_Collection()
    {
        var query = _db.Query("User")
            .Select(e => new Dictionary<string, object?>{
                { "Id", "Id" },
                { "Name", "Name" },
                { "Total-Members", e.Query("Members").Count("Id")},
            });
        var data = query.ToList();

        string[] expectedValues = [
           "Alice - 2",
            "Bob - 1",
            "Charlie - 0",
            "David - 2",
            "Eve - 0",
            "Frank - 0"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Total-Members"]}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void _20_Aggregate_Lookup_Collection()
    {
        var query = _db.Query("User")
            .Select(e => new Dictionary<string, object?>{
                { "Id", "Id" },
                { "Name", "Name" },
                { "Manager-Name", "Manager.Name" },
                { "Manager-Total-Members", e.Query("Manager.Members").Count("Id") },
            });
        var data = query.ToList();

        string[] expectedValues = [
           "Alice - NULL - 0",
            "Bob - Alice - 2",
            "Charlie - Alice - 2",
            "David - Bob - 1",
            "Eve - David - 2",
            "Frank - David - 2"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager-Name"] ?? "NULL"} - {e["Manager-Total-Members"]}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

}
