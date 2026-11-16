using System.Data;
using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest.Setup;

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
            Count(distinct Id) VisitCount,
            Count(distinct UserId) UniquePatients,
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
            Count(distinct Id) VisitCount,
            Count(distinct UserId) UniquePatients,
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
