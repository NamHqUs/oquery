using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using MySqlConnector;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
internal sealed class MariaQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetNorthStarMariaConnection();
        }
        catch (MySqlException exception)
        {
            Assert.Ignore($"MariaDB comparison requires the Docker MariaDB service: {exception.Message}");
        }

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(MetadataLoader.UnitTestData.Concat(MetadataLoader.NorthStarDemoData)),
            new FunctionProvider(new MariaDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    [Test]
    public void SavedNorthStarQueriesMatchMariaDbStatements()
    {
        var root = GetRepositoryRoot();
        var requestRoot = Path.Combine(
            root,
            "data-storage", "queries");
        var statements = LoadMariaStatements(Path.Combine(root, "data-storage", "scripts", "northstar-demo-queries.md"));
        var requests = Directory
            .EnumerateFiles(requestRoot, "*.json")
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.StartsWith("Sales ", StringComparison.Ordinal) ||
                    name.StartsWith("Health ", StringComparison.Ordinal) ||
                    name.StartsWith("Insurance ", StringComparison.Ordinal) ||
                    name.StartsWith("HR ", StringComparison.Ordinal);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(requests.Length, Is.EqualTo(20));
        Assert.That(statements.Count, Is.EqualTo(20));
        foreach (var statement in statements.Values)
            Assert.That(() => Execute(statement), Throws.Nothing);

        foreach (var requestPath in requests)
        {
            var reportName = Path.GetFileNameWithoutExtension(requestPath);
            var title = reportName[(reportName.IndexOf(' ') + 1)..];
            var query = QueryDefinitionLoader.Load(requestPath);

            var generatedRecords = _db.Query(query).ToList()
                .Select(record => record.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var rawRecords = Execute(statements[title]);

            AssertEquivalentRecords(generatedRecords, rawRecords, reportName);
        }
    }

    [Test]
    public void SampleSavedQueryDefinitionsExecuteOnMariaDb()
    {
        var root = Path.Combine(
            GetRepositoryRoot(), "data-storage", "queries");
        foreach (var requestPath in Directory.EnumerateFiles(root, "Sample-*.json").Order())
        {
            var query = QueryDefinitionLoader.Load(requestPath);
            Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, Path.GetFileName(requestPath));
        }
    }

    private IReadOnlyDictionary<string, string> LoadMariaStatements(string path)
    {
        var markdown = File.ReadAllText(path);
        var statements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match section in Regex.Matches(
                     markdown,
                     @"^### (?<title>.+?)\r?\n(?<body>.*?)(?=^### |\z)",
                     RegexOptions.Multiline | RegexOptions.Singleline))
        {
            var statement = Regex.Match(
                section.Groups["body"].Value,
                "=== \"MariaDB\"\\s*\\r?\\n\\s*```sql\\r?\\n(?<sql>.*?)\\r?\\n\\s*```",
                RegexOptions.Singleline);
            if (!statement.Success)
                throw new InvalidDataException(
                    $"MariaDB SQL statement is missing for report [{section.Groups["title"].Value}].");

            statements.Add(
                section.Groups["title"].Value.Trim(),
                Unindent(statement.Groups["sql"].Value));
        }

        return statements;
    }

    private IReadOnlyList<Dictionary<string, object?>> Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var records = new List<Dictionary<string, object?>>();

        while (reader.Read())
        {
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                record.Add(reader.GetName(index), reader.IsDBNull(index) ? null : reader.GetValue(index));
            records.Add(record);
        }

        return records;
    }

    private static void AssertEquivalentRecords(
        IReadOnlyList<Dictionary<string, object?>> actual,
        IReadOnlyList<Dictionary<string, object?>> expected,
        string reportName)
    {
        Assert.That(actual, Has.Count.EqualTo(expected.Count), reportName);
        var unmatched = expected.ToList();
        foreach (var actualRecord in actual)
        {
            var matchIndex = unmatched.FindIndex(expectedRecord =>
                expectedRecord.Keys.Count == actualRecord.Keys.Count &&
                expectedRecord.Keys.All(key =>
                    actualRecord.ContainsKey(key) &&
                    ValuesMatch(actualRecord[key], expectedRecord[key])));
            Assert.That(matchIndex, Is.GreaterThanOrEqualTo(0), reportName);
            unmatched.RemoveAt(matchIndex);
        }
    }

    private static bool ValuesMatch(object? actual, object? expected)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;

        if (IsNumber(actual) && IsNumber(expected))
        {
            var actualNumber = Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
            var expectedNumber = Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
            return Math.Abs(actualNumber - expectedNumber) <= 0.01m;
        }

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumber(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or
            float or double or decimal;

    private static string Unindent(string sql)
        => string.Join(
            Environment.NewLine,
            sql.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line));

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
