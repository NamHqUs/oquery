using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
internal sealed class QueryDefinitionFormatParityTests : TestBase
{
    [Test]
    public void PairedYamlAndJsonDefinitionsGenerateTheSameSqlStatement()
    {
        var queryDirectory = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");

        var yamlFiles = Directory.EnumerateFiles(queryDirectory, "*.yml")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.That(yamlFiles, Is.Not.Empty);

        var failures = new List<string>();
        foreach (var yamlPath in yamlFiles)
        {
            var jsonPath = Path.ChangeExtension(yamlPath, ".json");
            if (!File.Exists(jsonPath))
            {
                failures.Add($"{Path.GetFileName(yamlPath)}: matching JSON file is missing.");
                continue;
            }

            var yamlQuery = QueryDefinitionParser.ParseFromYml(new FileInfo(yamlPath));
            var jsonQuery = QueryDefinitionLoader.Load(jsonPath);
            var yamlSql = _db.Query(yamlQuery).ToSql().CommandText;
            var jsonSql = _db.Query(jsonQuery).ToSql().CommandText;

            if (!string.Equals(yamlSql, jsonSql, StringComparison.Ordinal))
            {
                failures.Add(
                    $"{Path.GetFileName(yamlPath)}:{Environment.NewLine}" +
                    $"YAML: {yamlSql}{Environment.NewLine}" +
                    $"JSON: {jsonSql}");
            }
        }

        Assert.That(
            failures,
            Is.Empty,
            "YAML and JSON query definitions must generate identical SqlStatement text.");
    }

    [Test]
    public void SavedDefinitionsRoundTripThroughBothParsersWithTheSameSql()
    {
        var queryDirectory = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");

        foreach (var jsonPath in Directory.EnumerateFiles(queryDirectory, "*.json").Order())
        {
            var yamlPath = Path.ChangeExtension(jsonPath, ".yml");
            Assert.That(File.Exists(yamlPath), Is.True, $"Missing YAML query request for '{jsonPath}'.");

            var json = QueryDefinitionLoader.Load(jsonPath);
            var yaml = YamlQueryDefinitionParser.Parse(new FileInfo(yamlPath));
            var jsonRoundTrip = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(
                System.Text.Json.JsonSerializer.Serialize(
                    json,
                    JsonQueryDefinitionParser.CreateSerializerOptions()),
                JsonQueryDefinitionParser.CreateSerializerOptions())
                ?? throw new InvalidDataException($"JSON query '{jsonPath}' could not round-trip.");
            var yamlRoundTrip = YamlQueryDefinitionParser.Parse(
                YamlQueryDefinitionParser.Write(yaml));

            var expectedSql = _db.Query(json).ToSql().CommandText;
            Assert.That(_db.Query(yaml).ToSql().CommandText, Is.EqualTo(expectedSql), jsonPath);
            Assert.That(_db.Query(jsonRoundTrip).ToSql().CommandText, Is.EqualTo(expectedSql), jsonPath);
            Assert.That(_db.Query(yamlRoundTrip).ToSql().CommandText, Is.EqualTo(expectedSql), jsonPath);
        }
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
