using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using OQuery.Test.TestData;
using Npgsql;
using OQuery;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestCockroachExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetNorthStarCockroachConnection();
        }
        catch (NpgsqlException exception)
        {
            Assert.Ignore($"CockroachDB comparison requires the Docker CockroachDB service: {exception.Message}");
        }

        var metadata = MetadataLoader.UnitTestData.Concat(MetadataLoader.NorthStarDemoData);
        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(new CockroachDialect()),
            new CockroachDialect());
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [Test]
    public void SavedQueriesMatchCockroachStatementsAndResults()
    {
        var root = Path.Combine(
            GetRepositoryRoot(), "data-storage", "queries");
        var markdown = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "data-storage", "scripts", "northstar-demo-queries.md"));

        var requests = Directory.EnumerateFiles(root, "*.json")
            .Where(path => Regex.IsMatch(Path.GetFileNameWithoutExtension(path), "^(Sales|Health|Insurance|HR) \\d+\\."))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(requests, Has.Count.EqualTo(20));
        foreach (var requestPath in requests)
        {
            var request = QueryDefinitionLoader.Load(requestPath);
            var name = Path.GetFileNameWithoutExtension(requestPath);
            var rawSql = FindCockroachStatement(markdown, name);
            Assert.That(rawSql, Is.Not.Null.And.Not.Empty, name);

            var generated = _db.Query(request).ToList();
            var expected = ExecuteRaw(rawSql!);
            Assert.That(
                generated.Select(Normalize).OrderBy(row => string.Join("|", row)).ToArray(),
                Is.EqualTo(expected.OrderBy(row => string.Join("|", row)).ToArray()),
                name);
        }
    }

    [Test]
    public void SkipWithoutTakeExecutesOnCockroach()
    {
        var query = _db.Query("Users").Select("Id").Skip(1);
        var commandText = query.ToSql().CommandText;

        Assert.That(commandText, Does.Contain(" OFFSET 1"));
        Assert.That(commandText, Does.Not.Contain("LIMIT"));
        Assert.DoesNotThrow(() => query.ToList());
    }

    [Test]
    public void SampleSavedQueryDefinitionsExecuteOnCockroach()
    {
        var root = Path.Combine(
            GetRepositoryRoot(), "data-storage", "queries");
        foreach (var requestPath in Directory.EnumerateFiles(root, "Sample-*.json").Order())
        {
            var query = QueryDefinitionLoader.Load(requestPath);
            Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, Path.GetFileName(requestPath));
        }
    }

    private IReadOnlyList<string[]> ExecuteRaw(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string[]>();
        while (reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index);
                var value = reader.IsDBNull(index)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
                row[index] = $"{name.ToLowerInvariant()}={NormalizeValue(value)}";
            }

            rows.Add([.. row.OrderBy(value => value[..value.IndexOf('=')], StringComparer.OrdinalIgnoreCase)]);
        }

        return rows;
    }

    private static string[] Normalize(Dictionary<string, object?> record)
        => record.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair =>
                $"{pair.Key.ToLowerInvariant()}={NormalizeValue(Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty)}")
            .ToArray();

    private static string NormalizeValue(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? decimal.Round(number, 2).ToString("0.00", CultureInfo.InvariantCulture)
            : value;

    private static string? FindCockroachStatement(string markdown, string name)
    {
        var title = Regex.Replace(name, "^(Sales|Health|Insurance|HR) ", string.Empty);
        var pattern = $@"(?ms)^###\s+{Regex.Escape(title)}\s*$.*?^=== ""CockroachDB""\s+```sql\s*(.*?)```";
        return Regex.Match(markdown, pattern).Groups[1].Value.Trim();
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
