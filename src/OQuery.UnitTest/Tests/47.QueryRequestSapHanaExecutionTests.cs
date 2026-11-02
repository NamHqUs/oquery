using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using OQuery;
using OQuery.Test.TestData;
using Sap.Data.Hana;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestSapHanaExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = DefinedData.GetNorthStarSapHanaConnection();
        }
        catch (HanaException exception)
        {
            Assert.Ignore($"SAP HANA comparison requires the Docker SAP HANA service: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            Assert.Ignore($"SAP HANA client rejected the configured connection: {exception.Message}");
        }

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(BuiltinMetadata.UnitTestData.Concat(BuiltinMetadata.NorthStarDemoData)),
            new FunctionProvider(new SapHanaDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    [Test]
    public void SavedNorthStarQueriesMatchSapHanaStatements()
    {
        var root = GetRepositoryRoot();
        var requestRoot = Path.Combine(root, "src", "OQuery.UnitTest", "TestData", "QueryRequests");
        var statements = LoadStatements(Path.Combine(root, "src", "OQuery.UnitTest", "TestData", "Scripts", "northstar-demo-queries.md"));
        var requests = Directory.EnumerateFiles(requestRoot, "*.json")
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
        foreach (var (title, statement) in statements)
            Assert.That(() => Execute(statement), Throws.Nothing, title);

        foreach (var requestPath in requests)
        {
            var reportName = Path.GetFileNameWithoutExtension(requestPath);
            var title = reportName[(reportName.IndexOf(' ') + 1)..];
            var query = SavedQueryDefinitionLoader.Load(requestPath);
            IReadOnlyList<Dictionary<string, object?>> generated;
            var generatedSql = string.Empty;
            try
            {
                var generatedQuery = _db.Query(query);
                generatedSql = generatedQuery.ToSql().CommandText;
                generated = generatedQuery.ToList()
                    .Select(record => record.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch (Exception exception)
            {
                Assert.Fail($"{reportName}: {exception.Message}\n{generatedSql}");
                return;
            }
            var raw = Execute(statements[title]);

            Assert.That(generated, Has.Count.EqualTo(raw.Count), reportName);
            foreach (var generatedRecord in generated)
            {
                Assert.That(
                    raw.Any(rawRecord =>
                        rawRecord.Keys.Count == generatedRecord.Keys.Count &&
                        rawRecord.Keys.All(key =>
                            generatedRecord.ContainsKey(key) &&
                            ValuesMatch(generatedRecord[key], rawRecord[key]))),
                    Is.True,
                    $"{reportName}: generated=[{string.Join(", ", generatedRecord.Select(pair => $"{pair.Key}={pair.Value}"))}] raw=[{string.Join(", ", raw.SelectMany(record => record.Select(pair => $"{pair.Key}={pair.Value}")))}]");
            }
        }
    }

    [Test]
    public void SampleSavedQueryDefinitionsExecuteOnSapHana()
    {
        var root = Path.Combine(
            GetRepositoryRoot(), "src", "OQuery.UnitTest", "TestData", "QueryRequests");
        foreach (var requestPath in Directory.EnumerateFiles(root, "Sample-*.json").Order())
        {
            if (Path.GetFileName(requestPath).Equals(
                    "Sample-Recursive employee hierarchy.json",
                    StringComparison.Ordinal))
            {
                TestContext.Progress.WriteLine(
                    $"{Path.GetFileName(requestPath)}: skipped - DBMS does not support recursive CTEs.");
                continue;
            }

            var query = SavedQueryDefinitionLoader.Load(requestPath);
            Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, Path.GetFileName(requestPath));
        }
    }

    private IReadOnlyDictionary<string, string> LoadStatements(string path)
    {
        var markdown = File.ReadAllText(path);
        var statements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match section in Regex.Matches(markdown, @"^### (?<title>.+?)\r?\n(?<body>.*?)(?=^### |\z)", RegexOptions.Multiline | RegexOptions.Singleline))
        {
            var statement = Regex.Match(
                section.Groups["body"].Value,
                "=== \"SAP HANA\"\\s*\\r?\\n\\s*```sql\\r?\\n(?<sql>.*?)\\r?\\n\\s*```",
                RegexOptions.Singleline);
            if (!statement.Success)
                throw new InvalidDataException($"SAP HANA SQL statement is missing for report [{section.Groups["title"].Value}].");
            statements.Add(section.Groups["title"].Value.Trim(), Unindent(statement.Groups["sql"].Value));
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

    private static bool ValuesMatch(object? actual, object? expected)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;
        if (decimal.TryParse(
                Convert.ToString(actual, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var actualNumber) &&
            decimal.TryParse(
                Convert.ToString(expected, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var expectedNumber))
        {
            return Math.Abs(actualNumber - expectedNumber) <= 0.01m;
        }
        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static string Unindent(string sql)
        => string.Join(Environment.NewLine, sql.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line));

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
