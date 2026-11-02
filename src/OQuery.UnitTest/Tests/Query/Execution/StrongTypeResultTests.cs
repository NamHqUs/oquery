using OQuery.UnitTest;

namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
public sealed class StrongTypeResultTests : TestBase
{
    [Test]
    public void ToListMaterializesDeclaredResultType()
    {
        var result = _db.Query("User")
            .Select("Id", "Name", "Age", "IsMale")
            .ToList<UserResult>();

        Assert.That(result, Has.Count.EqualTo(6));
        Assert.That(result[0], Is.EqualTo(new UserResult
        {
            Id = 1,
            Name = "Alice",
            Age = 45,
            IsMale = false
        }));
    }

    [Test]
    public async Task ToListAsyncMaterializesDeclaredResultType()
    {
        var result = await _db.Query("User")
            .Select("Id", "Name")
            .ToListAsync<UserResult>();

        Assert.That(result.Select(user => user.Name), Is.EquivalentTo(
            ["Alice", "Bob", "Charlie", "David", "Eve", "Frank"]));
    }

    [Test]
    public void ToListWithTotalRecordsMaterializesDeclaredResultType()
    {
        var (records, total) = _db.Query("User")
            .Select("Id", "Name")
            .TakeQuery(2)
            .ToListWithTotalRecords<UserResult>();

        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(total, Is.EqualTo(6));
        Assert.That(records[0].Name, Is.EqualTo("Alice"));
    }

    [Test]
    public void UnmappedResultFieldReportsAnActionableError()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _db.Query("User").Select("Id", "Name").ToList<IncompleteUserResult>());

        Assert.That(exception!.Message, Does.Contain("Name"));
        Assert.That(exception.Message, Does.Contain(nameof(IncompleteUserResult)));
    }

    private sealed class UserResult : IEquatable<UserResult>
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? Age { get; set; }
        public bool? IsMale { get; set; }

        public bool Equals(UserResult? other)
            => other is not null
                && Id == other.Id
                && Name == other.Name
                && Age == other.Age
                && IsMale == other.IsMale;

        public override bool Equals(object? obj) => Equals(obj as UserResult);
        public override int GetHashCode() => HashCode.Combine(Id, Name, Age, IsMale);
    }

    private sealed class IncompleteUserResult
    {
        public int Id { get; set; }
    }
}
