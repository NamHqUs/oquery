using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using IBM.Data.Db2;
using OQuery;
using OQuery.Test.TestData;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestDb2ExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;
    private IReadOnlyDictionary<string, string> _rawStatements = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
#if !NET10_0_OR_GREATER
        Assert.Ignore("Db2 execution requires the net10.0 test target because the IBM Db2 native client is not available on older targets.");
#endif
        try
        {
            _connection = DefinedData.GetNorthStarDb2Connection();
        }
        catch (DB2Exception exception)
        {
            Assert.Ignore($"Db2 execution requires the Docker Db2 service: {exception.Message}");
        }

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(BuiltinMetadata.UnitTestData.Concat(BuiltinMetadata.NorthStarDemoData)),
            new FunctionProvider(new Db2Dialect()),
            SqlDialect.ForConnection(_connection));
        _rawStatements = LoadStatements(Path.Combine(
            GetRepositoryRoot(),
            "scripts",
            "northstar-demo-queries.md"));
    }

    [OneTimeTearDown]
    public void TearDown() => _db?.Dispose();

    public static IEnumerable<TestCaseData> SavedQueryCases
        =>
        new[]
        {
            "Sales 1. Category and channel profitability.json",
            "Sales 2. Customer lifetime value and recency.json",
            "Sales 3. Sales representative performance.json",
            "Sales 4. Best-selling products and margin.json",
            "Sales 5. Monthly sales trend and momentum.json",
            "Health 1. Diagnosis burden by clinic.json",
            "Health 2. Physician workload and care-quality scorecard.json",
            "Health 3. High-risk patient care profile.json",
            "Health 4. Lab abnormality patterns.json",
            "Health 5. Visit-type utilization by month.json",
            "Insurance 1. Denial analysis by plan and reason.json",
            "Insurance 2. Plan profitability and medical loss ratio.json",
            "Insurance 3. Agent portfolio and premium collection.json",
            "Insurance 4. Claims adjudication by plan.json",
            "Insurance 5. Member utilization versus premium.json",
            "HR 1. Department compensation and performance.json",
            "HR 2. Manager span and team profile.json",
            "HR 3. Workforce composition by location.json",
            "HR 4. Leave demand and approval status.json",
            "HR 5. Performance and bonus trends.json"
        }
        .Select((fileName, index) =>
        {
            var queryName = Path.GetFileNameWithoutExtension(fileName);
            return new TestCaseData(fileName)
                .SetName($"SavedQueryGeneratesAndExecutesOnDb2_{index + 1:00}_{Regex.Replace(queryName, @"[^A-Za-z0-9]+", "_").Trim('_')}")
                .SetDescription(queryName);
        });

    [TestCaseSource(nameof(SavedQueryCases))]
    public void SavedQueryGeneratesAndExecutesOnDb2(string fileName)
    {
        var path = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            fileName);
        var query = SavedQueryDefinitionLoader.Load(path);
        var sql = _db.Query(query).ToSql().CommandText;
        var reportName = Path.GetFileNameWithoutExtension(fileName);
        var title = reportName[(reportName.IndexOf(' ') + 1)..];
        Assert.That(_rawStatements.TryGetValue(title, out var rawSql), Is.True);

        var rawRecords = Execute(rawSql!);
        var generatedRecords = _db.Query(query)
            .ToList()
            .Select(record => record.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.That(sql, Is.Not.Empty, "SQL generation returned an empty statement.");
        Assert.That(generatedRecords.Length, Is.EqualTo(rawRecords.Count), title);
        foreach (var generatedRecord in generatedRecords)
        {
            var matches = rawRecords.Any(rawRecord =>
                rawRecord.Keys.Count == generatedRecord.Keys.Count &&
                rawRecord.Keys.All(key =>
                    generatedRecord.ContainsKey(key) &&
                    ValuesMatch(generatedRecord[key], rawRecord[key])));
            if (!matches)
            {
                TestContext.Progress.WriteLine(
                    $"Generated record: {FormatRecord(generatedRecord)}");
                TestContext.Progress.WriteLine(
                    $"Raw records: {string.Join(" | ", rawRecords.Select(FormatRecord))}");
            }
            Assert.That(
                matches,
                Is.True,
                $"{title}\nGenerated SQL:\n{sql}");
        }
    }

    [Test]
    public void SampleSavedQueryDefinitionsExecuteOnDb2()
    {
        var root = Path.Combine(
            GetRepositoryRoot(), "src", "OQuery.UnitTest", "TestData", "QueryRequests");
        foreach (var requestPath in Directory.EnumerateFiles(root, "Sample-*.json").Order())
        {
            var query = SavedQueryDefinitionLoader.Load(requestPath);
            Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, Path.GetFileName(requestPath));
        }
    }

    private IReadOnlyDictionary<string, string> LoadStatements(string path)
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
                "=== \"Db2\".*?```sql\\s*(?<sql>.*?)\\s*```",
                RegexOptions.Singleline);
            if (!statement.Success)
                throw new InvalidDataException(
                    $"Db2 SQL statement is missing for report [{section.Groups["title"].Value}].");
            statements.Add(
                section.Groups["title"].Value.Trim(),
                NormalizeDb2NumericScale(Unindent(statement.Groups["sql"].Value)));
        }

        Assert.That(statements, Has.Count.EqualTo(20));
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
            var record = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                record.Add(
                    reader.GetName(index),
                    reader.IsDBNull(index) ? null : reader.GetValue(index));
            records.Add(record);
        }

        return records;
    }

    private static bool ValuesMatch(object? actual, object? expected)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;
        if (IsNumber(actual) && IsNumber(expected))
        {
            var difference = Math.Abs(
                Convert.ToDecimal(actual, CultureInfo.InvariantCulture) -
                Convert.ToDecimal(expected, CultureInfo.InvariantCulture));
            return difference <= 0.01m;
        }

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumber(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static string FormatRecord(IReadOnlyDictionary<string, object?> record)
        => string.Join(
            ", ",
            record.Select(pair =>
                $"{pair.Key}={Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? "<null>"}"));

    private static string Unindent(string sql)
        => string.Join(
            Environment.NewLine,
            sql.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => line.StartsWith("    ", StringComparison.Ordinal)
                    ? line[4..]
                    : line));

    private static string NormalizeDb2NumericScale(string sql)
        => sql
            .Replace(
                "100.0 *",
                "CAST(100.0 AS DECIMAL(31, 15)) *",
                StringComparison.Ordinal)
            .Replace(
                "(DATE '2025-10-31' - e.HireDate) / 365.25",
                "(DAYS(DATE '2025-10-31') - DAYS(e.HireDate)) / 365.25",
                StringComparison.Ordinal);

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
