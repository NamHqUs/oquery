using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
internal sealed class QueryDefinitionFormatParityTests : TestBase
{
    [Test]
    public void PairedOQueryAndJsonDefinitionsGenerateTheSameSqlStatement()
    {
        var queryDirectory = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");

        var oqueryFiles = Directory.EnumerateFiles(queryDirectory, "*.oqr")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        Assert.That(oqueryFiles, Has.Length.EqualTo(5));

        var failures = new List<string>();
        foreach (var oqueryPath in oqueryFiles)
        {
            var jsonPath = Path.ChangeExtension(oqueryPath, ".json");
            if (!File.Exists(jsonPath))
            {
                failures.Add($"{Path.GetFileName(oqueryPath)}: matching JSON file is missing.");
                continue;
            }

            var oquery = OQueryDefinitionParser.Parse(new FileInfo(oqueryPath));
            var jsonQuery = QueryDefinitionLoader.Load(jsonPath);
            var oquerySql = _db.Query(oquery).ToSql().CommandText;
            var jsonSql = _db.Query(jsonQuery).ToSql().CommandText;

            if (!string.Equals(oquerySql, jsonSql, StringComparison.Ordinal))
            {
                failures.Add(
                    $"{Path.GetFileName(oqueryPath)}:{Environment.NewLine}" +
                    $"OQuery: {oquerySql}{Environment.NewLine}" +
                    $"JSON: {jsonSql}");
            }
        }

        Assert.That(
            failures,
            Is.Empty,
            "OQuery and JSON query definitions must generate identical SqlStatement text.");
    }

    [Test]
    public void SavedDefinitionsRoundTripThroughOQueryAndJsonWithTheSameSql()
    {
        var queryDirectory = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");

        foreach (var oqueryPath in Directory.EnumerateFiles(queryDirectory, "*.oqr").Order().Take(5))
        {
            var jsonPath = Path.ChangeExtension(oqueryPath, ".json");
            Assert.That(File.Exists(jsonPath), Is.True, $"Missing JSON query request for '{oqueryPath}'.");

            var json = QueryDefinitionLoader.Load(jsonPath);
            var oquery = OQueryDefinitionParser.Parse(new FileInfo(oqueryPath));
            var jsonRoundTrip = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(
                System.Text.Json.JsonSerializer.Serialize(
                    json,
                    JsonQueryDefinitionParser.CreateSerializerOptions()),
                JsonQueryDefinitionParser.CreateSerializerOptions())
                ?? throw new InvalidDataException($"JSON query '{jsonPath}' could not round-trip.");
            var oqueryRoundTrip = OQueryDefinitionParser.Parse(
                OQueryDefinitionParser.Write(oquery));

            var expectedSql = _db.Query(json).ToSql().CommandText;
            Assert.That(_db.Query(oquery).ToSql().CommandText, Is.EqualTo(expectedSql), jsonPath);
            Assert.That(_db.Query(jsonRoundTrip).ToSql().CommandText, Is.EqualTo(expectedSql), jsonPath);
            Assert.That(_db.Query(oqueryRoundTrip).ToSql().CommandText, Is.EqualTo(expectedSql), jsonPath);
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
