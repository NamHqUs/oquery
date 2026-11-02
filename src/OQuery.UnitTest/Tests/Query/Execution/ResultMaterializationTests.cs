namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
public sealed class ResultMaterializationTests
{
    [Test]
    public void ProvidesIndexedTypedAndDictionaryAccess()
    {
        var result = new OQueryResult(
            [
                new OQueryResultField("Id", typeof(int)),
                new OQueryResultField("Name", typeof(string))
            ],
            [
                new object?[] { 1, "Alice" },
                new object?[] { 2, "Bob" }
            ]);

        Assert.That(result[0, 0], Is.EqualTo(1));
        Assert.That(result[0]["Name"], Is.EqualTo("Alice"));
        Assert.That(result[0, "Name"], Is.EqualTo("Alice"));
        Assert.That(result["Name", 1], Is.EqualTo("Bob"));
        Assert.That(result.As<int>(0, "Id"), Is.EqualTo(1));
        Assert.That(result.As<string>("Name"), Is.EqualTo(["Alice", "Bob"]));

        Assert.That(result.ToDictionary(0)["Name"], Is.EqualTo("Alice"));
        Assert.That(result.ToDictionary(), Has.Count.EqualTo(2));
        Assert.That(result.Select(row => row["Name"]), Is.EqualTo(["Alice", "Bob"]));
    }
}
