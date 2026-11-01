using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using OQuery.Contracts;
using OQuery.Test.TestData;

namespace OQuery.Test.Tests;

[TestFixture]
internal sealed class QueryRequestSqlComparisonTests
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
            _connection = DefinedData.GetDbConnection(DbServerType.SqlServer);
        }
        catch (SqlException exception)
        {
            Assert.Ignore($"SQL Server comparison requires the Docker SQL Server: {exception.Message}");
        }

        var metadata = BuiltinMetadata.UnitTestData
            .Concat(BuiltinMetadata.SaleDemoData)
            .Concat(BuiltinMetadata.NorthStarDemoData);

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

    [Test]
    public void CategoryAndChannelProfitabilityMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sales 1. Category and channel profitability.json");
        var request = JsonSerializer.Deserialize<StoredQueryRequest>(
            File.ReadAllText(requestPath),
            JsonOptions)
            ?? throw new InvalidDataException($"Query request '{requestPath}' is empty.");
        var rawSql = QualifyNorthStarTables(
            ExtractSqlServerStatement("Category and channel profitability"));
        var mainQuery = request.MainQuery;

        var contexts = request.Contexts
            .Select(context => new QueryContextDefintion(
                context.Name,
                new QueryDefinition
                {
                    Entity = context.Entity,
                    Projection = context.Projection,
                    Filter = context.Filters,
                    Sorts = context.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
                    Page = context.Page,
                    PageSize = context.PageSize
                },
                null,
                QueryJoinType.Inner))
            .ToList();

        var query = new QueryDefinition
        {
            Entity = mainQuery.Entity,
            Projection = mainQuery.Projection,
            Filter = mainQuery.Filters,
            Sorts = mainQuery.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
            Page = mainQuery.Page,
            PageSize = mainQuery.PageSize,
            Contexts = contexts
        };

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(rawSql);

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void CustomerLifetimeValueAndRecencyMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sales 2. Customer lifetime value and recency.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Customer lifetime value and recency")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void SalesRepresentativePerformanceMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sales 3. Sales representative performance.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Sales representative performance")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void BestSellingProductsAndMarginMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sales 4. Best-selling products and margin.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Best-selling products and margin")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void MonthlySalesTrendAndMomentumMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sales 5. Monthly sales trend and momentum.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Monthly sales trend and momentum")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void DiagnosisBurdenByClinicMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Health 1. Diagnosis burden by clinic.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList()
            .OrderByDescending(record => Convert.ToDecimal(record["VisitCount"], CultureInfo.InvariantCulture))
            .ThenByDescending(record => Convert.ToDecimal(record["AbnormalLabPercent"], CultureInfo.InvariantCulture))
            .ThenBy(record => record["Diagnosis"]?.ToString())
            .ThenBy(record => record["ClinicCity"]?.ToString())
            .ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Diagnosis burden by clinic")
                    .Replace(
                        "ORDER BY VisitCount DESC, AbnormalLabPercent DESC;",
                        "ORDER BY VisitCount DESC, AbnormalLabPercent DESC, d.Name ASC, l.City ASC;",
                        StringComparison.Ordinal)));
        sqlServerRecords = sqlServerRecords
            .OrderByDescending(record => Convert.ToDecimal(record["VisitCount"], CultureInfo.InvariantCulture))
            .ThenByDescending(record => Convert.ToDecimal(record["AbnormalLabPercent"], CultureInfo.InvariantCulture))
            .ThenBy(record => record["Diagnosis"]?.ToString())
            .ThenBy(record => record["ClinicCity"]?.ToString())
            .ToList();

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void DiagnosisBurdenByClinicYamlGeneratesTheSameSqlAsJson()
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests");
        var jsonQuery = SavedQueryDefinitionLoader.Load(
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
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests");

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

            var jsonSql = _db.Query(SavedQueryDefinitionLoader.Load(jsonPath)).ToSql();
            var yamlSql = _db.Query(QueryDefinition.FromYml(File.ReadAllText(yamlPath))).ToSql();

            Assert.That(NormalizeSql(jsonSql), Is.EqualTo(NormalizeSql(yamlSql)), jsonPath);
        }
    }

    [Test]
    public void RecursiveEmployeeHierarchyYamlMatchesJsonDefinition()
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests");
        var jsonPath = Path.Combine(requestRoot, "Sample-Recursive employee hierarchy.json");
        var yamlPath = Path.ChangeExtension(jsonPath, ".yml");

        var jsonDefinition = SavedQueryDefinitionLoader.Load(jsonPath);
        var yamlDefinition = QueryDefinition.FromYml(File.ReadAllText(yamlPath));

        Assert.That(yamlDefinition.ToYml(), Is.EqualTo(jsonDefinition.ToYml()));
    }

    [TestCase("Test-Check Context Calculation Fields.json")]
    [TestCase("Test-Query directly on REF CONTEXT.json")]
    public void RefContextYamlMatchesJsonDefinition(string fileName)
    {
        var requestRoot = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests");
        var jsonPath = Path.Combine(requestRoot, fileName);
        var yamlPath = Path.ChangeExtension(jsonPath, ".yml");

        var jsonDefinition = SavedQueryDefinitionLoader.Load(jsonPath);
        var yamlDefinition = QueryDefinition.FromYml(File.ReadAllText(yamlPath));

        Assert.That(yamlDefinition.ToYml(), Is.EqualTo(jsonDefinition.ToYml()), fileName);
    }

    [Test]
    public void PhysicianWorkloadAndCareQualityScorecardMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Health 2. Physician workload and care-quality scorecard.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Physician workload and care-quality scorecard")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void HighRiskPatientCareProfileMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Health 3. High-risk patient care profile.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("High-risk patient care profile")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void LabAbnormalityPatternsMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Health 4. Lab abnormality patterns.json");
        var request = JsonSerializer.Deserialize<StoredQueryRequest>(
            File.ReadAllText(requestPath),
            JsonOptions)
            ?? throw new InvalidDataException($"Query request '{requestPath}' is empty.");
        var mainQuery = request.MainQuery;
        var contexts = request.Contexts
            .Select(context => new QueryContextDefintion(
                context.Name,
                new QueryDefinition
                {
                    Entity = context.Entity,
                    Projection = context.Projection,
                    Filter = context.Filters,
                    Sorts = context.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
                    Page = context.Page,
                    PageSize = context.PageSize
                },
                null,
                QueryJoinType.Inner))
            .ToList();
        var query = new QueryDefinition
        {
            Entity = mainQuery.Entity,
            Projection = mainQuery.Projection,
            Filter = mainQuery.Filters,
            Sorts = mainQuery.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
            Page = mainQuery.Page,
            PageSize = mainQuery.PageSize,
            Contexts = contexts
        };

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Lab abnormality patterns")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void VisitTypeUtilizationByMonthMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Health 5. Visit-type utilization by month.json");
        var request = JsonSerializer.Deserialize<StoredQueryRequest>(
            File.ReadAllText(requestPath),
            JsonOptions)
            ?? throw new InvalidDataException($"Query request '{requestPath}' is empty.");
        var mainQuery = request.MainQuery;
        var contexts = request.Contexts
            .Select(context => new QueryContextDefintion(
                context.Name,
                new QueryDefinition
                {
                    Entity = context.Entity,
                    Projection = context.Projection,
                    Filter = context.Filters,
                    Sorts = context.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
                    Page = context.Page,
                    PageSize = context.PageSize
                },
                null,
                QueryJoinType.Inner))
            .ToList();
        var query = new QueryDefinition
        {
            Entity = mainQuery.Entity,
            Projection = mainQuery.Projection,
            Filter = mainQuery.Filters,
            Sorts = mainQuery.Sorts?.SelectMany(sorts => sorts).ToList() ?? [],
            Page = mainQuery.Page,
            PageSize = mainQuery.PageSize,
            Contexts = contexts
        };

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Visit-type utilization by month")
                    .Replace(
                        "GROUP BY VisitMonth, VisitType",
                        "GROUP BY CONVERT(char(7), VisitDate, 126), VisitType",
                        StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void DenialAnalysisByPlanAndReasonMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Insurance 1. Denial analysis by plan and reason.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Denial analysis by plan and reason")
                    .Replace("AS Plan", "AS [Plan]", StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void PlanProfitabilityAndMedicalLossRatioMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Insurance 2. Plan profitability and medical loss ratio.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Plan profitability and medical loss ratio")
                    .Replace("AS Plan", "AS [Plan]", StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void AgentPortfolioAndPremiumCollectionMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Insurance 3. Agent portfolio and premium collection.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Agent portfolio and premium collection")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void ClaimsAdjudicationByPlanMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Insurance 4. Claims adjudication by plan.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Claims adjudication by plan")
                    .Replace("AS Plan", "AS [Plan]", StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void MemberUtilizationVersusPremiumMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Insurance 5. Member utilization versus premium.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Member utilization versus premium")
                    .Replace("AS Plan", "AS [Plan]", StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void DepartmentCompensationAndPerformanceMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "HR 1. Department compensation and performance.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Department compensation and performance")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void ManagerSpanAndTeamProfileMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "HR 2. Manager span and team profile.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Manager span and team profile")));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void LeaveDemandAndApprovalStatusMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "HR 4. Leave demand and approval status.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Leave demand and approval status")
                    .Replace(
                        "ORDER BY DaysRequested DESC, Requests DESC;",
                        "ORDER BY DaysRequested DESC, Requests DESC, d.Name ASC, lr.LeaveType ASC, lr.Status ASC;",
                        StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void WorkforceCompositionByLocationMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "HR 3. Workforce composition by location.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Workforce composition by location")
                    .Replace(
                        "ORDER BY Headcount DESC, AnnualPayroll DESC;",
                        "ORDER BY Headcount DESC, AnnualPayroll DESC, l.City ASC, l.State ASC;",
                        StringComparison.Ordinal)));
        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void PerformanceAndBonusTrendsMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "HR 5. Performance and bonus trends.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            QualifyNorthStarTables(
                ExtractSqlServerStatement("Performance and bonus trends")
                    .Replace(
                        "ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC;",
                        "ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC, d.Name ASC;",
                        StringComparison.Ordinal)));

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void SampleNestedCustomerMonthlyPeakByRegionMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sample-Nested customer monthly peak by region.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            "WITH customer_month AS (" +
            "SELECT o.UserId, YEAR(o.OrderDate) AS SalesYear, MONTH(o.OrderDate) AS SalesMonth, " +
            "SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS MonthlyRevenue " +
            "FROM [sa].[Orders] o " +
            "JOIN [sa].[OrderItems] oi ON oi.OrderId = o.Id " +
            "WHERE o.Status = 'Completed' " +
            "GROUP BY o.UserId, YEAR(o.OrderDate), MONTH(o.OrderDate)), " +
            "customer_best_month AS (" +
            "SELECT UserId, MAX(MonthlyRevenue) AS BestMonthRevenue " +
            "FROM customer_month GROUP BY UserId) " +
            "SELECT u.State AS Region, AVG(cb.BestMonthRevenue) AS AverageBestMonth " +
            "FROM [Main].[Users] u " +
            "JOIN customer_best_month cb ON cb.UserId = u.Id " +
            "GROUP BY u.State;");
        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void SampleCorrelatedTopThreeOrdersPerCustomerMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sample-Correlated top three orders per customer.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);
        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            "WITH ranked_orders AS (" +
            "SELECT o.UserId, o.Id, o.OrderDate, " +
            "SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS OrderAmount, " +
            "ROW_NUMBER() OVER (PARTITION BY o.UserId ORDER BY o.OrderDate DESC) AS OrderRank " +
            "FROM [sa].[Orders] o " +
            "JOIN [sa].[OrderItems] oi ON oi.OrderId = o.Id " +
            "WHERE o.Status = 'Completed' " +
            "GROUP BY o.UserId, o.Id, o.OrderDate), " +
            "recent_orders AS (" +
            "SELECT UserId, Id, OrderDate, OrderAmount " +
            "FROM ranked_orders WHERE OrderRank <= 3) " +
            "SELECT u.Id AS CustomerId, CONCAT(u.FirstName, ' ', u.LastName) AS Customer, " +
            "r.Id AS OrderId, r.OrderDate, r.OrderAmount " +
            "FROM [Main].[Users] u " +
            "JOIN recent_orders r ON r.UserId = u.Id " +
            "ORDER BY u.Id ASC, r.OrderDate DESC, r.Id ASC;");

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void SampleUnionCompletedOrdersAndActivePoliciesMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sample-Union completed orders and active policies.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList()
            .Select(record => Convert.ToInt32(record["UserId"], CultureInfo.InvariantCulture))
            .ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            "SELECT UserId FROM [sa].[Orders] WHERE Status = 'Completed' " +
            "UNION " +
            "SELECT UserId FROM [in].[Policies] WHERE Status = 'Active';")
            .Select(record => Convert.ToInt32(record["UserId"], CultureInfo.InvariantCulture))
            .ToList();

        Assert.That(dynamicRecords, Is.EquivalentTo(sqlServerRecords));
    }

    [Test]
    public void SampleRecursiveEmployeeHierarchyExecutesAsRecursiveContext()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sample-Recursive employee hierarchy.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var records = _db.Query(query).ToList();

        Assert.That(records, Is.Not.Empty);
        Assert.That(records.Select(record => record["Level"]), Does.Contain(0));
    }

    [Test]
    public void SampleCorrelatedRevenueSincePolicyStartMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sample-Correlated revenue since policy start.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            "SELECT po.Id AS PolicyId, CONCAT(u.FirstName, ' ', u.LastName) AS Member, " +
            "COALESCE(SUM(CASE WHEN pp.Status = 'Paid' AND pp.PaymentDate >= po.StartDate " +
            "THEN pp.Amount END), 0) AS RevenueSinceSignup " +
            "FROM [in].[Policies] po " +
            "JOIN [Main].[Users] u ON u.Id = po.UserId " +
            "LEFT JOIN [in].[PremiumPayments] pp ON pp.PolicyId = po.Id " +
            "GROUP BY po.Id, u.FirstName, u.LastName " +
            "ORDER BY po.Id;");

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    [Test]
    public void SampleDepartmentSalaryRankMatchesSqlServerData()
    {
        var requestPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "OQuery.UnitTest",
            "TestData",
            "QueryRequests",
            "Sample-Department salary rank.json");
        var query = SavedQueryDefinitionLoader.Load(requestPath);

        var dynamicRecords = _db.Query(query).ToList();
        var sqlServerRecords = ExecuteSqlServerQuery(
            "SELECT DepartmentId, Id AS EmployeeId, Salary, " +
            "RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) AS DepartmentRank " +
            "FROM [Main].[Employees] " +
            "ORDER BY DepartmentId ASC, DepartmentRank ASC, EmployeeId ASC;");

        Assert.That(dynamicRecords, Has.Count.EqualTo(sqlServerRecords.Count));
        for (var rowIndex = 0; rowIndex < dynamicRecords.Count; rowIndex++)
        {
            var actual = dynamicRecords[rowIndex].ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var expected = sqlServerRecords[rowIndex];

            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"Dictionary<string, object?> {rowIndex}");
            foreach (var (column, expectedValue) in expected)
            {
                AssertValuesMatch(
                    actual[column],
                    expectedValue,
                    $"Dictionary<string, object?> {rowIndex}, column '{column}'");
            }
        }
    }

    private IReadOnlyList<Dictionary<string, object?>> ExecuteSqlServerQuery(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var records = new List<Dictionary<string, object?>>();

        while (reader.Read())
        {
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
            {
                record.Add(
                    reader.GetName(columnIndex),
                    reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex));
            }

            records.Add(record);
        }

        return records;
    }

    private static void AssertValuesMatch(object? actual, object? expected, string context)
    {
        if (IsNumber(actual) && IsNumber(expected))
        {
            var actualNumber = Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
            var expectedNumber = Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
            var tolerance = IsIntegral(actual) && IsIntegral(expected) ? 0m : 0.01m;
            Assert.That(Math.Abs(actualNumber - expectedNumber), Is.LessThanOrEqualTo(tolerance), context);
            return;
        }

        Assert.That(actual, Is.EqualTo(expected), context);
    }

    private static bool IsNumber(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or
            float or double or decimal;

    private static bool IsIntegral(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong;

    private static string QualifyNorthStarTables(string sql)
        => sql
            .Replace("FROM Users ", "FROM [Main].[Users] ", StringComparison.Ordinal)
            .Replace("JOIN Users ", "JOIN [Main].[Users] ", StringComparison.Ordinal)
            .Replace("FROM Employees ", "FROM [Main].[Employees] ", StringComparison.Ordinal)
            .Replace("JOIN Employees ", "JOIN [Main].[Employees] ", StringComparison.Ordinal)
            .Replace("FROM Departments ", "FROM [hr].[Departments] ", StringComparison.Ordinal)
            .Replace("JOIN Departments ", "JOIN [hr].[Departments] ", StringComparison.Ordinal)
            .Replace("FROM PerformanceReviews", "FROM [hr].[PerformanceReviews]", StringComparison.Ordinal)
            .Replace("JOIN PerformanceReviews", "JOIN [hr].[PerformanceReviews]", StringComparison.Ordinal)
            .Replace("FROM LeaveRequests", "FROM [hr].[LeaveRequests]", StringComparison.Ordinal)
            .Replace("JOIN LeaveRequests", "JOIN [hr].[LeaveRequests]", StringComparison.Ordinal)
            .Replace("FROM Locations ", "FROM [Main].[Locations] ", StringComparison.Ordinal)
            .Replace("JOIN Locations ", "JOIN [Main].[Locations] ", StringComparison.Ordinal)
            .Replace("FROM Visits", "FROM [he].[Visits]", StringComparison.Ordinal)
            .Replace("JOIN Visits", "JOIN [he].[Visits]", StringComparison.Ordinal)
            .Replace("FROM Diagnoses ", "FROM [he].[Diagnoses] ", StringComparison.Ordinal)
            .Replace("JOIN Diagnoses ", "JOIN [he].[Diagnoses] ", StringComparison.Ordinal)
            .Replace("FROM LabResults", "FROM [he].[LabResults]", StringComparison.Ordinal)
            .Replace("JOIN LabResults", "JOIN [he].[LabResults]", StringComparison.Ordinal)
            .Replace("FROM Claims", "FROM [in].[Claims]", StringComparison.Ordinal)
            .Replace("JOIN Claims", "JOIN [in].[Claims]", StringComparison.Ordinal)
            .Replace("FROM Policies", "FROM [in].[Policies]", StringComparison.Ordinal)
            .Replace("JOIN Policies", "JOIN [in].[Policies]", StringComparison.Ordinal)
            .Replace("FROM PremiumPayments", "FROM [in].[PremiumPayments]", StringComparison.Ordinal)
            .Replace("JOIN PremiumPayments", "JOIN [in].[PremiumPayments]", StringComparison.Ordinal)
            .Replace("FROM InsurancePlans", "FROM [in].[InsurancePlans]", StringComparison.Ordinal)
            .Replace("JOIN InsurancePlans", "JOIN [in].[InsurancePlans]", StringComparison.Ordinal)
            .Replace("FROM Orders ", "FROM [sa].[Orders] ", StringComparison.Ordinal)
            .Replace("JOIN Orders ", "JOIN [sa].[Orders] ", StringComparison.Ordinal)
            .Replace("FROM OrderItems ", "FROM [sa].[OrderItems] ", StringComparison.Ordinal)
            .Replace("JOIN OrderItems ", "JOIN [sa].[OrderItems] ", StringComparison.Ordinal)
            .Replace("FROM Products ", "FROM [sa].[Products] ", StringComparison.Ordinal)
            .Replace("JOIN Products ", "JOIN [sa].[Products] ", StringComparison.Ordinal);

    private static string ExtractSqlServerStatement(string title)
    {
        var markdown = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OQuery.UnitTest", "TestData", "Scripts", "northstar-demo-queries.md"));
        var lines = markdown.Split('\n');
        var headingIndex = Array.FindIndex(
            lines,
            line => line.TrimEnd('\r').EndsWith($". {title}", StringComparison.Ordinal));
        if (headingIndex < 0)
            throw new InvalidDataException($"No query section named '{title}' was found.");

        var sectionEnd = Array.FindIndex(
            lines,
            headingIndex + 1,
            line => line.StartsWith("### ", StringComparison.Ordinal) ||
                line.StartsWith("## ", StringComparison.Ordinal));
        if (sectionEnd < 0)
            sectionEnd = lines.Length;

        var sqlServerIndex = Array.FindIndex(
            lines,
            headingIndex,
            sectionEnd - headingIndex,
            line => line.Trim() == "=== \"SQL Server\"");
        var codeStart = Array.FindIndex(
            lines,
            sqlServerIndex + 1,
            sectionEnd - sqlServerIndex - 1,
            line => line.Trim() == "```sql");
        var codeEnd = Array.FindIndex(
            lines,
            codeStart + 1,
            sectionEnd - codeStart - 1,
            line => line.Trim() == "```");

        if (sqlServerIndex < 0 || codeStart < 0 || codeEnd < 0)
            throw new InvalidDataException($"Query section '{title}' has no complete SQL Server statement.");

        return string.Join(
            Environment.NewLine,
            lines[(codeStart + 1)..codeEnd]
                .Select(line => line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line))
            .Trim()
            .TrimEnd(';');
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

    private sealed class StoredQueryRequest
    {
        public required MainQuery MainQuery { get; init; }
        public List<StoredContext> Contexts { get; init; } = [];
    }

    private sealed class MainQuery
    {
        public required string Entity { get; init; }
        public List<ProjectionDefinition> Projection { get; init; } = [];
        public ConditionDefinition? Filters { get; init; }
        public List<List<SortDefinition>>? Sorts { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
    }

    private sealed class StoredContext
    {
        public required string Name { get; init; }
        public required string Entity { get; init; }
        public List<ProjectionDefinition> Projection { get; init; } = [];
        public ConditionDefinition? Filters { get; init; }
        public List<List<SortDefinition>>? Sorts { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
    }
}
