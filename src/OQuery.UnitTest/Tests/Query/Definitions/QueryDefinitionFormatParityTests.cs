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

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;
}
