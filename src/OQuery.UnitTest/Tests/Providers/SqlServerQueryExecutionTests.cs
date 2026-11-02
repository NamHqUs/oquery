using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
internal sealed class SqlServerQueryExecutionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetDbConnection(DbServerType.SqlServer);
        }
        catch (Exception exception) when (exception is SqlException or ArgumentException)
        {
            Assert.Ignore($"SQL Server comparison requires the Docker SQL Server: {exception.Message}");
        }

        var metadata = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.SaleDemoData)
            .Concat(MetadataLoader.NorthStarDemoData);

        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(SqlDialect.ForConnection(_connection)),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnSqlServer(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }

    [Test]
    public void DiagnosisBurdenByClinicYamlGeneratesTheSameSqlAsJson()
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");
        var jsonQuery = QueryDefinitionLoader.Load(
            Path.Combine(requestRoot, "Health 1. Diagnosis burden by clinic.json"));
        var yamlPath = Path.Combine(requestRoot, "Health 1. Diagnosis burden by clinic.yml");
        var yamlQuery = QueryDefinition.FromYml(File.ReadAllText(yamlPath));

        var jsonSql = _db.Query(jsonQuery).ToSql();
        var yamlSql = _db.Query(yamlQuery).ToSql();

        Assert.That(NormalizeSql(jsonSql), Is.EqualTo(NormalizeSql(yamlSql)));
    }

    [Test]
    public void AllYamlQueryRequestsGenerateTheSameSqlAsJson()
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");

        foreach (var jsonPath in Directory.EnumerateFiles(requestRoot, "*.json").Order())
        {
            var fileName = Path.GetFileName(jsonPath);
            if (fileName.Equals("Sample-Recursive employee hierarchy.json", StringComparison.Ordinal) ||
                fileName.Equals("Test-Check Context Calculation Fields.json", StringComparison.Ordinal) ||
                fileName.Equals("Test-Query directly on REF CONTEXT.json", StringComparison.Ordinal))
            {
                continue;
            }

            var yamlPath = Path.ChangeExtension(jsonPath, ".yml");
            Assert.That(File.Exists(yamlPath), Is.True, $"Missing YAML query request for '{jsonPath}'.");

            var jsonSql = _db.Query(QueryDefinitionLoader.Load(jsonPath)).ToSql();
            var yamlSql = _db.Query(QueryDefinition.FromYml(File.ReadAllText(yamlPath))).ToSql();

            Assert.That(NormalizeSql(jsonSql), Is.EqualTo(NormalizeSql(yamlSql)), jsonPath);
        }
    }

    [Test]
    public void RecursiveEmployeeHierarchyYamlMatchesJsonDefinition()
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");
        var jsonPath = Path.Combine(requestRoot, "Sample-Recursive employee hierarchy.json");
        var yamlPath = Path.ChangeExtension(jsonPath, ".yml");

        var jsonDefinition = QueryDefinitionLoader.Load(jsonPath);
        var yamlDefinition = QueryDefinition.FromYml(File.ReadAllText(yamlPath));

        Assert.That(yamlDefinition.ToYml(), Is.EqualTo(jsonDefinition.ToYml()));
    }

    [TestCase("Test-Check Context Calculation Fields.json")]
    [TestCase("Test-Query directly on REF CONTEXT.json")]
    public void RefContextYamlMatchesJsonDefinition(string fileName)
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries");
        var jsonPath = Path.Combine(requestRoot, fileName);
        var yamlPath = Path.ChangeExtension(jsonPath, ".yml");

        var jsonDefinition = QueryDefinitionLoader.Load(jsonPath);
        var yamlDefinition = QueryDefinition.FromYml(File.ReadAllText(yamlPath));

        Assert.That(yamlDefinition.ToYml(), Is.EqualTo(jsonDefinition.ToYml()), fileName);
    }

    [Test]
    public void SampleRecursiveEmployeeHierarchyExecutesAsRecursiveContext()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "data-storage", "queries",
            "Sample-Recursive employee hierarchy.json");
        var query = QueryDefinitionLoader.Load(requestPath);

        var records = _db.Query(query).ToList();

        Assert.That(records, Is.Not.Empty);
        Assert.That(records.Select(record => record["Level"]), Does.Contain(0));
    }

    private static string GetRepositoryRoot()
        => new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "../../../../../")).FullName;

    private static string NormalizeSql(SqlTranslationResult query)
    {
        var commandText = Regex.Replace(
            query.CommandText,
            @"^DECLARE [^\r\n]+;\r?\n",
            string.Empty,
            RegexOptions.Multiline);

        foreach (var parameter in query.Parameters)
        {
            commandText = commandText.Replace(
                parameter.Name,
                FormatSqlLiteral(parameter.Value),
                StringComparison.Ordinal);
        }

        return commandText;
    }

    private static string FormatSqlLiteral(object? value)
        => value switch
        {
            null => "NULL",
            bool boolean => boolean ? "1" : "0",
            string text => $"'{text.Replace("'", "''", StringComparison.Ordinal)}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
}
