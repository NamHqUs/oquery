using System.Data;
using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest.Setup;
using OQuery.UnitTest.Tests.Providers;

namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
internal sealed class SqliteQueryExecutionTests
{
    private IDbConnection _connection = null!;
    private OQueryEngine _db = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _connection = TestDatabase.GetNorthStarSqliteDockerConnection();
        var metadata = MetadataLoader.UnitTestData
            .Concat(MetadataLoader.NorthStarDemoData);
        _db = new OQueryEngine(
            _connection,
            new MetadataProvider(metadata),
            new FunctionProvider(new SqliteDialect()),
            SqlDialect.ForConnection(_connection));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    [TestCaseSource(typeof(SavedQueryTestCases), nameof(SavedQueryTestCases.Cases))]
    public void SavedQueryExecutesOnSqlite(string fileName)
    {
        var query = QueryDefinitionLoader.Load(SavedQueryTestCases.GetRequestPath(fileName));
        Assert.That(() => _db.Query(query).ToList(), Throws.Nothing, fileName);
    }

    [Test]
    public void DiagnosisBurdenSavedQueryMatchesNativeSqliteResults()
    {
        var oqueryPath = SavedQueryTestCases.GetRequestPath("Health 1. Diagnosis burden by clinic.oqr");

        const string nativeSql = """
            WITH visit_labs AS (
                SELECT VisitId, COUNT(*) AS LabCount,
                       SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
                FROM he.LabResults
                GROUP BY VisitId)
            SELECT d.Name, d.Category, l.City,
                   COUNT(DISTINCT v.Id), COUNT(DISTINCT v.UserId), ROUND(AVG(v.Charge), 2),
                   ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                         / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
            FROM he.Visits v
            JOIN he.Diagnoses d ON d.Id = v.DiagnosisId
            JOIN main.Locations l ON l.Id = v.LocationId
            LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
            WHERE v.Status = 'Completed'
            GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
            ORDER BY COUNT(DISTINCT v.Id) DESC, 7 DESC
            LIMIT 10
            """;

        var actual = _db.Query(QueryDefinitionOQueryParser.Parse(new FileInfo(oqueryPath)))
            .ToList()
            .OrderBy(row => (string)row["Diagnosis"]!)
            .ThenBy(row => (string)row["ClinicCity"]!)
            .ToList();

        var expected = new List<Dictionary<string, object?>>();
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = nativeSql;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                expected.Add(new Dictionary<string, object?>
                {
                    ["Diagnosis"] = reader.GetString(0),
                    ["DiagnosisCategory"] = reader.GetString(1),
                    ["ClinicCity"] = reader.GetString(2),
                    ["VisitCount"] = reader.GetInt64(3),
                    ["UniquePatients"] = reader.GetInt64(4),
                    ["AverageCharge"] = reader.GetDouble(5),
                    ["AbnormalLabPercent"] = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                });
            }
        }

        expected = expected
            .OrderBy(row => (string)row["Diagnosis"]!)
            .ThenBy(row => (string)row["ClinicCity"]!)
            .ToList();

        Assert.That(actual, Has.Count.EqualTo(expected.Count));
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual[i]["Diagnosis"], Is.EqualTo(expected[i]["Diagnosis"]), $"row {i}");
                Assert.That(actual[i]["ClinicCity"], Is.EqualTo(expected[i]["ClinicCity"]), $"row {i}");
                Assert.That(Convert.ToInt64(actual[i]["VisitCount"]), Is.EqualTo(expected[i]["VisitCount"]), $"row {i}");
                Assert.That(Convert.ToInt64(actual[i]["UniquePatients"]), Is.EqualTo(expected[i]["UniquePatients"]), $"row {i}");
                Assert.That(Convert.ToDouble(actual[i]["AverageCharge"]), Is.EqualTo((double)expected[i]["AverageCharge"]!).Within(0.01), $"row {i}");
                Assert.That(Convert.ToDouble(actual[i]["AbnormalLabPercent"]), Is.EqualTo((double)expected[i]["AbnormalLabPercent"]!).Within(0.01), $"row {i}");
            });
        }
    }

    private const string SeparateLabContextScorecard = """
        DoctorLabs is LabResults:
            Visit.DoctorId DoctorId,
            Count(Id) LabCount,
            CountIf(IsAbnormal = <True>) AbnormalCount
          Where Visit.Status = <Completed>

        PhysicianScorecard is Employees:
          Left Join DoctorLabs On Id = DoctorLabs.DoctorId
            Select
            Id DoctorId,
            PhysicianVisits.CountIf(Status = <Completed>) VisitCount,
            PhysicianVisits.SumIf(Charge, Status = <Completed>) Charges,
            (<100> * DoctorLabs.AbnormalCount / NullIf(DoctorLabs.LabCount, <0>)).Round(<2>) AbnormalLabPercent
        """;

    private const string MergedScorecard = """
        PhysicianScorecard is Employees:
            Id DoctorId,
            PhysicianVisits.CountIf(Status = <Completed>) VisitCount,
            PhysicianVisits.SumIf(Charge, Status = <Completed>) Charges,
            (<100> * PhysicianVisits.SumIf(LabResults.CountIf(IsAbnormal = <True>), Status = <Completed>)
                / NullIf(PhysicianVisits.SumIf(LabResults.Count(Id), Status = <Completed>), <0>)).Round(<2>) AbnormalLabPercent
        """;

    [Test]
    public void PhysicianScorecardMergedQueryMatchesSeparateLabContextResults()
    {
        var separate = _db.Query(QueryDefinitionOQueryParser.Parse(SeparateLabContextScorecard))
            .ToList()
            .ToDictionary(row => Convert.ToInt64(row["DoctorId"]));
        var merged = _db.Query(QueryDefinitionOQueryParser.Parse(MergedScorecard))
            .ToList()
            .ToDictionary(row => Convert.ToInt64(row["DoctorId"]));

        Assert.That(separate, Is.Not.Empty);
        Assert.That(separate.Values.Any(row => row["AbnormalLabPercent"] is not null and not DBNull), Is.True);
        Assert.That(merged.Keys, Is.EquivalentTo(separate.Keys));
        foreach (var (doctorId, expected) in separate)
        {
            var actual = merged[doctorId];
            Assert.Multiple(() =>
            {
                Assert.That(Convert.ToInt64(actual["VisitCount"]), Is.EqualTo(Convert.ToInt64(expected["VisitCount"])), $"doctor {doctorId}");
                Assert.That(RoundOrNull(actual["Charges"]), Is.EqualTo(RoundOrNull(expected["Charges"])), $"doctor {doctorId}");
                Assert.That(RoundOrNull(actual["AbnormalLabPercent"]), Is.EqualTo(RoundOrNull(expected["AbnormalLabPercent"])), $"doctor {doctorId}");
            });
        }
    }

    private const string SeparateDiagnosisBurden = """
        VisitLabs is LabResults:
            VisitId,
            Count(Id) LabCount,
            CountIf(IsAbnormal = <True>) AbnormalCount

        DiagnosisBurden is Visits:
          Left Join VisitLabs On Id = VisitLabs.VisitId
          Select
            Diagnosis.Name DiagnosisName,
            Diagnosis.Category DiagnosisCategory,
            Location.City ClinicCity,
            CountDistinct(Id) VisitCount,
            CountDistinct(UserId) UniquePatients,
            Avg(Charge).Round(<2>) AverageCharge,
            (<100.0> * Sum(IfNull(VisitLabs.AbnormalCount, <0>))
                / NullIf(Sum(IfNull(VisitLabs.LabCount, <0>)), <0>)).Round(<2>) AbnormalLabPercent
          Where Status = <Completed>
        """;

    private const string MergedDiagnosisBurden = """
        DiagnosisBurden is Visits:
            Diagnosis.Name DiagnosisName,
            Diagnosis.Category DiagnosisCategory,
            Location.City ClinicCity,
            CountDistinct(Id) VisitCount,
            CountDistinct(UserId) UniquePatients,
            Avg(Charge).Round(<2>) AverageCharge,
            (<100.0> * Sum(LabResults.CountIf(IsAbnormal = <True>))
                / NullIf(Sum(LabResults.Count(Id)), <0>)).Round(<2>) AbnormalLabPercent
          Where Status = <Completed>
        """;

    [Test]
    public void DiagnosisBurdenMergedQueryMatchesSeparateLabContextResults()
    {
        var separate = _db.Query(QueryDefinitionOQueryParser.Parse(SeparateDiagnosisBurden))
            .ToList()
            .ToDictionary(row => (DiagnosisName: (string)row["DiagnosisName"]!, ClinicCity: (string)row["ClinicCity"]!));
        var merged = _db.Query(QueryDefinitionOQueryParser.Parse(MergedDiagnosisBurden))
            .ToList()
            .ToDictionary(row => (DiagnosisName: (string)row["DiagnosisName"]!, ClinicCity: (string)row["ClinicCity"]!));

        Assert.That(separate, Is.Not.Empty);
        Assert.That(separate.Values.Any(row => row["AbnormalLabPercent"] is not null and not DBNull), Is.True);
        Assert.That(merged.Keys, Is.EquivalentTo(separate.Keys));
        foreach (var (key, expected) in separate)
        {
            var actual = merged[key];
            Assert.Multiple(() =>
            {
                Assert.That(Convert.ToInt64(actual["VisitCount"]), Is.EqualTo(Convert.ToInt64(expected["VisitCount"])), $"{key}");
                Assert.That(Convert.ToInt64(actual["UniquePatients"]), Is.EqualTo(Convert.ToInt64(expected["UniquePatients"])), $"{key}");
                Assert.That(RoundOrNull(actual["AverageCharge"]), Is.EqualTo(RoundOrNull(expected["AverageCharge"])), $"{key}");
                Assert.That(RoundOrNull(actual["AbnormalLabPercent"]), Is.EqualTo(RoundOrNull(expected["AbnormalLabPercent"])), $"{key}");
            });
        }
    }

    private static double? RoundOrNull(object? value)
        => value is null or DBNull ? null : Math.Round(Convert.ToDouble(value), 2);
}
