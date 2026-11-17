using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest.Setup;

namespace OQuery.UnitTest.Tests.Providers;

[TestFixture]
[NonParallelizable]
internal sealed class NorthStarDomainSqlServerTests
{
    private const string SalesDomain = "Sales_Domain.json";
    private const string HealthcareDomain = "HEALTHCARE_DOMAIN.json";
    private const string InsuranceDomain = "INSURANCE_DOMAIN.json";
    private const string HrDomain = "HR_Domain.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly IReadOnlyDictionary<string, (string Domain, string OQuery)> Reports = new Dictionary<string, (string, string)>
    {
        ["Sales 1"] = (SalesDomain, """
            OrderItems: Product.Category Category,
                Order.Channel Channel,
                Count(distinct Order.Id) OrdersContainingCategory,
                Sum(Quantity) UnitsSold,
                Sum(Quantity * UnitPrice - DiscountAmount).Round(<2>) NetRevenue,
                Sum(DiscountAmount).Round(<2>) Discounts,
                Sum(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount).Round(<2>) GrossProfit,
                (Sum(Quantity * UnitPrice - DiscountAmount) / NullIf(Count(distinct Order.Id), <0>)).Round(<2>) AverageCategoryRevenuePerOrder
              Where Order.Status = <Completed>
              Order GrossProfit DESC
              Take 10
            """),
        ["Sales 2"] = (SalesDomain, """
            Customers: Id UserId,
                Concat(FirstName, < >, LastName) Customer,
                Orders.CountIf(Status = <Completed>) OrderCount,
                Orders.SumIf(Items.Sum(Quantity * UnitPrice - DiscountAmount), Status = <Completed>).Round(<2>) LifetimeValue,
                Orders.MaxIf(OrderDate, Status = <Completed>) LastOrderDate,
                Orders.CountIf(distinct Channel, Status = <Completed>) ChannelsUsed
              Where OrderCount > <1>
              Order LifetimeValue DESC, LastOrderDate DESC
              Take 10
            """),
        ["Sales 3"] = (SalesDomain, """
            Seller: Id SalesRepId,
                Concat(FirstName, < >, LastName) SalesRep,
                Location.City HomeCity,
                Department.Name Department,
                Orders.CountIf(Status = <Completed>) CompletedOrders,
                Orders.SumIf(Items.Sum(Quantity), Status = <Completed>) UnitsSold,
                Orders.SumIf(Items.Sum(Quantity * UnitPrice - DiscountAmount), Status = <Completed>).Round(<2>) NetSales,
                Orders.SumIf(Items.Sum(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount), Status = <Completed>).Round(<2>) GrossProfit
              Where Department.Name = <Retail Sales> AND CompletedOrders > <0>
              Order NetSales DESC
              Take 10
            """),
        ["Sales 4"] = (SalesDomain, """
            Products: Id ProductId,
                Name Product,
                Category,
                Items.SumIf(Quantity, Order.Status = <Completed>) UnitsSold,
                Items.CountIf(distinct Order.Id, Order.Status = <Completed>) OrderCount,
                Items.SumIf(Quantity * UnitPrice - DiscountAmount, Order.Status = <Completed>).Round(<2>) NetRevenue,
                Items.SumIf(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount, Order.Status = <Completed>).Round(<2>) GrossProfit,
                (100.0 * GrossProfit / NullIf(NetRevenue, <0>)).Round(<2>) MarginPercent
              Where UnitsSold > <0>
              Order GrossProfit DESC
              Take 10
            """),
        ["Sales 5"] = (SalesDomain, """
            Orders:
                MonthKey(OrderDate) SalesMonth,
                Sum(Items.Sum(Quantity * UnitPrice - DiscountAmount)).Round(<2>) Revenue,
                Count() OrderCount,
                Lag(Revenue, <1>, SalesMonth).Round(<2>) PriorMonthRevenue,
                (Revenue - PriorMonthRevenue).Round(<2>) RevenueChange,
                WindowSum(Revenue, SalesMonth).Round(<2>) RunningRevenue
              Where Status = <Completed>
              Order SalesMonth DESC
              Take 10
            """),
        ["Healthcare 1"] = (HealthcareDomain, """
            Visits: Diagnose.Name Diagnosis,
                Diagnose.Category DiagnosisCategory,
                Location.City ClinicCity,
                Count(distinct Id) VisitCount,
                Count(distinct Patient.Id) UniquePatients,
                Avg(Charge).Round(<2>) AverageCharge,
                (100.0 * Sum(LabResults.CountIf(IsAbnormal = <True>)) / NullIf(Sum(LabResults.Count()), <0>)).Round(<2>) AbnormalLabPercent
              Where Status = <Completed>
              Order VisitCount DESC, AbnormalLabPercent DESC
              Take 10
            """),
        ["Healthcare 2"] = (HealthcareDomain, """
            Doctors: Id DoctorId,
                Concat(FirstName, < >, LastName) Physician,
                Department.Name Department,
                Location.City ClinicCity,
                Examinations.CountIf(Status = <Completed>) VisitCount,
                Examinations.CountIf(distinct Patient.Id, Status = <Completed>) PatientCount,
                Examinations.AvgIf(DurationMinutes, Status = <Completed>).Round(<1>) AverageVisitMinutes,
                Examinations.SumIf(Charge, Status = <Completed>).Round(<2>) Charges,
                (100.0 * Examinations.SumIf(LabResults.CountIf(IsAbnormal = <True>), Status = <Completed>)
                    / NullIf(Examinations.SumIf(LabResults.Count(), Status = <Completed>), <0>)).Round(<2>) AbnormalLabPercent
              Where Department.Name In <Primary Care,Specialty Care,Emergency Medicine>
              Order VisitCount DESC, Charges DESC
              Take 10
            """),
        ["Healthcare 3"] = (HealthcareDomain, """
            Patients: Id PatientId,
                Concat(FirstName, < >, LastName) Patient,
                BirthDate.YearsTo(<2025-10-31>) Age,
                Visits.CountIf(Status = <Completed> AND Diagnose.IsChronic = <True>) ChronicVisits,
                Visits.CountIf(Status = <Completed>) CompletedVisits,
                Visits.SumIf(LabResults.CountIf(IsAbnormal = <True>), Status = <Completed>) AbnormalResults,
                Visits.SumIf(Charge, Status = <Completed>).Round(<2>) TotalCharges,
                Visits.MaxIf(VisitDate, Status = <Completed>) LastVisitDate
              Where ChronicVisits > <0> AND AbnormalResults > <0>
              Order AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
              Take 10
            """),
        ["Healthcare 4"] = (HealthcareDomain, """
            LabResults: TestName,
                Visit.Diagnose.Category DiagnosisCategory,
                Count() TestCount,
                CountIf(IsAbnormal = <True>) AbnormalCount,
                (100.0 * CountIf(IsAbnormal = <True>) / Count()).Round(<2>) AbnormalPercent,
                Avg(ResultValue).Round(<2>) AverageResult,
                Avg(ReferenceMax).Round(<2>) AverageReferenceMaximum
              Where Visit.Status = <Completed> AND TestCount >= <5>
              Order AbnormalPercent DESC, TestCount DESC
              Take 10
            """),
        ["Healthcare 5"] = (HealthcareDomain, """
            Visits: MonthKey(VisitDate) VisitMonth,
                VisitType,
                Count() ScheduledVisits,
                CountIf(Status = <No-Show>) NoShows,
                (100.0 * CountIf(Status = <No-Show>) / Count()).Round(<2>) NoShowPercent,
                AvgIf(DurationMinutes, Status = <Completed>).Round(<1>) AverageMinutes,
                Sum(Charge).Round(<2>) Charges
              Order ScheduledVisits DESC
              Take 10
            """),
        ["Insurance 1"] = (InsuranceDomain, """
            Claims: Policy.InsurancePlan.Tier Tier,
                Policy.InsurancePlan.Name Plan,
                DenialReason,
                Count() DeniedClaims,
                Sum(BilledAmount).Round(<2>) BilledAmount,
                Sum(AllowedAmount).Round(<2>) AllowedAmount,
                Avg(BilledAmount).Round(<2>) AverageBilledAmount
              Where Status = <Denied>
              Order DeniedClaims DESC, BilledAmount DESC
              Take 10
            """),
        ["Insurance 2"] = (InsuranceDomain, """
            InsurancePlans: Name Plan,
                Tier,
                Policies.Count() PolicyCount,
                Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)).Round(<2>) PremiumRevenue,
                Policies.Sum(Claims.Count()) ClaimCount,
                Policies.Sum(Claims.Sum(PaidAmount)).Round(<2>) ClaimsPaid,
                Policies.Sum(Claims.CountIf(Status = <Denied>)) DeniedClaims,
                (100.0 * ClaimsPaid / NullIf(PremiumRevenue, <0>)).Round(<2>) MedicalLossRatioPercent
              Order MedicalLossRatioPercent DESC
              Take 10
            """),
        ["Insurance 3"] = (InsuranceDomain, """
            Agents: Id AgentId,
                Concat(FirstName, < >, LastName) Agent,
                Location.City OfficeCity,
                Policies.Count() PoliciesSold,
                Policies.CountIf(Status = <Active>) ActivePolicies,
                Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)).Round(<2>) PaidPremiums,
                Policies.Sum(PremiumPayments.CountIf(Status = <Late>)) LatePayments,
                Policies.Sum(PremiumPayments.CountIf(Status = <Missed>)) MissedPayments
              Where Department.Name = <Insurance Agency>
              Order PaidPremiums DESC
              Take 10
            """),
        ["Insurance 4"] = (InsuranceDomain, """
            InsurancePlans: Name Plan,
                Tier,
                Policies.Sum(Claims.Count()) ClaimsReceived,
                Policies.Sum(Claims.CountIf(Status = <Paid>)) PaidClaims,
                Policies.Sum(Claims.CountIf(Status = <Denied>)) DeniedClaims,
                (100.0 * DeniedClaims / NullIf(ClaimsReceived, <0>)).Round(<2>) DenialPercent,
                (Policies.Sum(Claims.Sum(DateSub(ClaimDate, ProcessedDate))) * 1.0
                    / NullIf(Policies.Sum(Claims.Count(ProcessedDate)), <0>)).Round(<1>) AverageProcessingDays,
                Policies.Sum(Claims.Sum(PaidAmount)).Round(<2>) TotalPaid
              Where ClaimsReceived > <0>
              Order ClaimsReceived DESC
              Take 10
            """),
        ["Insurance 5"] = (InsuranceDomain, """
            Clients: Id MemberId,
                Concat(FirstName, < >, LastName) Member,
                (IfNull(Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)), <0>)).Round(<2>) PremiumsCollected,
                (IfNull(Policies.Sum(Claims.Sum(AllowedAmount)), <0>)).Round(<2>) AllowedAmount,
                (IfNull(Policies.Sum(Claims.Sum(PaidAmount)), <0>)).Round(<2>) InsurerPaid,
                IfNull(Policies.Sum(Claims.Count(Id)), <0>) Claims,
                (100.0 * InsurerPaid / NullIf(PremiumsCollected, <0>)).Round(<2>) MemberLossRatioPercent
              Where Policies.Count(Id) > <0>
              Order MemberLossRatioPercent DESC, Claims DESC
              Take 10
            """),
        ["HR 1"] = (HrDomain, """
            Departments: Name Department,
                Employees.Count(Id) Headcount,
                Employees.Avg(Salary).Round(<2>) AverageSalary,
                Employees.Sum(Salary).Round(<2>) AnnualPayroll,
                Employees.Avg(PerformanceReviews.AvgIf(Rating, ReviewYear = <2025>)).Round(<2>) AverageRating,
                Employees.Sum(PerformanceReviews.SumIf(BonusAmount, ReviewYear = <2025>)).Round(<2>) TotalBonuses
              Where Headcount > <0>
              Order AnnualPayroll DESC
              Take 10
            """),
        ["HR 2"] = (HrDomain, """
            Employees: Id ManagerId,
                Concat(FirstName, < >, LastName) Manager,
                Department.Name Department,
                Members.Count(Id) DirectReports,
                Members.Count(distinct LocationId) TeamLocations,
                Members.Avg(Salary).Round(<2>) AverageTeamSalary,
                (Members.Sum(PerformanceReviews.SumIf(Rating, ReviewYear = <2025>)) * 1.0
                    / NullIf(Members.Sum(PerformanceReviews.CountIf(ReviewYear = <2025>)), <0>)).Round(<2>) AverageTeamRating,
                Members.Sum(Salary).Round(<2>) TeamPayroll
              Where DirectReports > <0>
              Order DirectReports DESC, TeamPayroll DESC
              Take 10
            """),
        ["HR 3"] = (HrDomain, """
            Locations: City OfficeCity,
                State,
                Employees.Count(Id) Headcount,
                Employees.Count(distinct DepartmentId) DepartmentsRepresented,
                Employees.CountIf(EmploymentType = <Full-time>) FullTime,
                Employees.CountIf(EmploymentType = <Part-time>) PartTime,
                Employees.CountIf(EmploymentType = <Contract>) Contractors,
                Employees.Avg(HireDate.ElapsedYears(<2025-10-31>)).Round(<1>) AverageTenureYears,
                Employees.Sum(Salary).Round(<2>) AnnualPayroll
              Where Headcount > <0>
              Order Headcount DESC, AnnualPayroll DESC
              Take 10
            """),
        ["HR 4"] = (HrDomain, """
            LeaveRequests: Employee.Department.Name Department,
                LeaveType,
                Status,
                Count() Requests,
                Sum(Days) DaysRequested,
                Avg(Days).Round(<1>) AverageRequestDays,
                Count(distinct EmployeeId) EmployeesRequesting
              Order DaysRequested DESC, Requests DESC
              Take 10
            """),
        ["HR 5"] = (HrDomain, """
            PerformanceReviews: Employee.Department.Name Department,
                ReviewYear,
                Count() Reviews,
                Avg(Rating).Round(<2>) AverageRating,
                CountIf(Rating >= <4>) HighRatings,
                (100.0 * CountIf(Rating >= <4>) / Count()).Round(<2>) HighRatingPercent,
                Avg(Employee.Salary).Round(<2>) AverageSalary,
                Sum(BonusAmount).Round(<2>) TotalBonus
              Order ReviewYear DESC, AverageRating DESC, TotalBonus DESC
              Take 10
            """),
    };

    // Declared grain for each report: the root entity and what one result row represents.
    private static readonly IReadOnlyDictionary<string, (string Root, string OneRowIs)> ReportGrains = new Dictionary<string, (string, string)>
    {
        ["Sales 1"] = ("OrderItems", "one category and channel"),
        ["Sales 2"] = ("Customers", "one customer"),
        ["Sales 3"] = ("Seller", "one sales representative"),
        ["Sales 4"] = ("Products", "one product"),
        ["Sales 5"] = ("Orders", "one month"),
        ["Healthcare 1"] = ("Visits", "one diagnosis and clinic"),
        ["Healthcare 2"] = ("Doctors", "one physician"),
        ["Healthcare 3"] = ("Patients", "one patient"),
        ["Healthcare 4"] = ("LabResults", "one test and diagnosis category"),
        ["Healthcare 5"] = ("Visits", "one month and visit type"),
        ["Insurance 1"] = ("Claims", "one plan tier, plan and denial reason"),
        ["Insurance 2"] = ("InsurancePlans", "one plan"),
        ["Insurance 3"] = ("Agents", "one agent"),
        ["Insurance 4"] = ("InsurancePlans", "one plan"),
        ["Insurance 5"] = ("Clients", "one member"),
        ["HR 1"] = ("Departments", "one department"),
        ["HR 2"] = ("Employees", "one manager"),
        ["HR 3"] = ("Locations", "one office"),
        ["HR 4"] = ("LeaveRequests", "one department, leave type and status"),
        ["HR 5"] = ("PerformanceReviews", "one department and review year"),
    };

    private static readonly Dictionary<string, string> DomainHeadings = new(StringComparer.OrdinalIgnoreCase)    {
        ["Sales"] = SalesDomain,
        ["Health care"] = HealthcareDomain,
        ["Insurance"] = InsuranceDomain,
        ["Human resources"] = HrDomain,
    };

    private static readonly IReadOnlyDictionary<string, string[]> KnownReferenceDeviations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        // The reference SQL uses AVG over int columns, which SQL Server truncates; OQuery returns the decimal average.
        ["HR 2"] = ["AverageTeamRating"],
        ["Insurance 4"] = ["AverageProcessingDays"],
        // Member-grain report: the reference returns one row per policy with its plan and status.
        ["Insurance 5"] = ["Plan", "PolicyStatus"],
    };

    private IDbConnection _connection = null!;
    private Dictionary<string, OQueryEngine> _engines = null!;
    private Dictionary<string, string> _referenceSql = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            _connection = TestDatabase.GetDbConnection(DbServerType.SqlServer);
        }
        catch (Exception exception) when (exception is SqlException or ArgumentException)
        {
            Assert.Ignore($"NorthStar SQL Server tests require the Docker SQL Server: {exception.Message}");
        }

        _engines = new Dictionary<string, OQueryEngine>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(GetDomainsRoot(), "*.json"))
        {
            _engines[Path.GetFileName(file)] = new OQueryEngine(
                _connection,
                new MetadataProvider(LoadDomain(file)),
                new FunctionProvider(SqlDialect.ForConnection(_connection)),
                SqlDialect.ForConnection(_connection));
        }

        _referenceSql = LoadReferenceSql();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        if (_engines is null)
            return;

        foreach (var engine in _engines.Values)
            engine.Dispose();
    }

    public static IEnumerable<string> ReportKeys() => Reports.Keys;

    [TestCaseSource(nameof(ReportKeys))]
    public void ReportRootMatchesDeclaredGrain(string report)
    {
        Assert.That(ReportGrains.ContainsKey(report), Is.True, $"{report}: declare its grain in ReportGrains.");
        Assert.That(ReportGrains[report].OneRowIs, Is.Not.Empty, $"{report}: grain must describe one result row.");

        var query = QueryDefinitionOQueryParser.Parse(Reports[report].OQuery);

        Assert.That(query.Entity, Is.EqualTo(ReportGrains[report].Root), $"{report}: root entity must match its declared grain.");
    }

    [Test]
    public void PaidPremiumIsCountedOnceAtPlanGrain()
    {
        const string text = """
            InsurancePlans: Id PlanId,
                Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)) PaidPremium
              Where Id = <8>
            """;

        var rows = _engines[InsuranceDomain].Query(QueryDefinitionOQueryParser.Parse(text)).ToList();

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(Convert.ToDecimal(rows[0]["PaidPremium"], CultureInfo.InvariantCulture), Is.EqualTo(116290m));
    }

    [TestCaseSource(nameof(ReportKeys))]
    public void OQueryReportExecutesOnSqlServer(string report)
    {
        var rows = ExecuteOQuery(report);

        Assert.That(rows, Is.Not.Empty, report);
        Assert.That(rows.Count, Is.LessThanOrEqualTo(10), report);
    }

    [TestCaseSource(nameof(ReportKeys))]
    public void OQueryReportMatchesSqlReference(string report)
    {
        if (!_referenceSql.TryGetValue(report, out var sql))
            Assert.Inconclusive($"No SQL reference found for '{report}' in the markdown source.");

        List<Dictionary<string, object?>> reference;
        try
        {
            reference = ExecuteSql(sql);
        }
        catch (SqlException exception)
        {
            Assert.Inconclusive($"The SQL reference for '{report}' does not run on SQL Server: {exception.Message}");
            return;
        }

        var oquery = ExecuteOQuery(report);

        var ignoredColumns = KnownReferenceDeviations.TryGetValue(report, out var known) ? known : Array.Empty<string>();
        var expectedColumns = reference.Count > 0 ? reference[0].Keys.ToList() : new List<string>();
        var actualColumns = oquery.Count > 0 ? oquery[0].Keys.ToList() : new List<string>();
        var missingColumns = expectedColumns
            .Where(column => !ignoredColumns.Any(ignored => string.Equals(ignored, column, StringComparison.OrdinalIgnoreCase)))
            .Where(column => !actualColumns.Any(actual => string.Equals(actual, column, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.That(missingColumns, Is.Empty, $"{report}: columns missing from the OQuery result");

        var columns = actualColumns
            .Where(column => expectedColumns.Any(expected => string.Equals(expected, column, StringComparison.OrdinalIgnoreCase)))
            .Where(column => !ignoredColumns.Any(ignored => string.Equals(ignored, column, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(oquery.Count, Is.EqualTo(reference.Count), $"{report}: row count");

        // Rows are compared after sorting on their values, so ties in ORDER BY do not produce false differences.
        var expectedRows = reference.OrderBy(row => SortKey(row, columns), StringComparer.Ordinal).ToList();
        var actualRows = oquery.OrderBy(row => SortKey(row, columns), StringComparer.Ordinal).ToList();
        var differences = new List<string>();
        for (var row = 0; row < expectedRows.Count; row++)
        {
            foreach (var column in columns)
            {
                if (!ValuesMatch(expectedRows[row][column], actualRows[row][column]))
                    differences.Add($"row {row + 1} [{column}]: sql={expectedRows[row][column] ?? "NULL"} oquery={actualRows[row][column] ?? "NULL"}");
            }
        }

        if (ignoredColumns.Length > 0)
            TestContext.Out.WriteLine($"{report}: known reference deviation, not compared: {string.Join(", ", ignoredColumns)}.");

        Assert.That(differences, Is.Empty, $"{report}: {string.Join("; ", differences)}");
    }

    // Other roots that answer the same business question. Each must return the same rows as the report.
    private static readonly IReadOnlyDictionary<string, (string Domain, string OQuery)> ReportAlternatives = new Dictionary<string, (string, string)>
    {
        ["Sales 5"] = (SalesDomain, """
            OrderItems:
                MonthKey(Order.OrderDate) SalesMonth,
                Sum(Quantity * UnitPrice - DiscountAmount).Round(<2>) Revenue,
                Count(distinct Order.Id) OrderCount,
                Lag(Revenue, <1>, SalesMonth).Round(<2>) PriorMonthRevenue,
                (Revenue - PriorMonthRevenue).Round(<2>) RevenueChange,
                WindowSum(Revenue, SalesMonth).Round(<2>) RunningRevenue
              Where Order.Status = <Completed>
              Order SalesMonth DESC
              Take 10
            """),
        ["Insurance 4"] = (InsuranceDomain, """
            Claims: Policy.InsurancePlan.Name Plan,
                Policy.InsurancePlan.Tier Tier,
                Count() ClaimsReceived,
                CountIf(Status = <Paid>) PaidClaims,
                CountIf(Status = <Denied>) DeniedClaims,
                (100.0 * DeniedClaims / NullIf(ClaimsReceived, <0>)).Round(<2>) DenialPercent,
                (Sum(DateSub(ClaimDate, ProcessedDate)) * 1.0 / NullIf(Count(ProcessedDate), <0>)).Round(<1>) AverageProcessingDays,
                Sum(PaidAmount).Round(<2>) TotalPaid
              Order ClaimsReceived DESC
              Take 10
            """),
    };

    public static IEnumerable<string> AlternativeKeys() => ReportAlternatives.Keys;

    [TestCaseSource(nameof(AlternativeKeys))]
    public void AlternativeRootReturnsTheSameRows(string report)
    {
        var (domain, text) = ReportAlternatives[report];
        var alternative = _engines[domain].Query(QueryDefinitionOQueryParser.Parse(text)).ToList()
            .Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var ignored = KnownReferenceDeviations.TryGetValue(report, out var known) ? known : Array.Empty<string>();

        AssertSameRows(alternative, ExecuteOQuery(report), ignored, report);
    }

    private static void AssertSameRows(
        IReadOnlyList<Dictionary<string, object?>> expectedRows,
        IReadOnlyList<Dictionary<string, object?>> actualRows,
        IEnumerable<string> ignoredColumns,
        string label)
    {
        var ignored = ignoredColumns.ToArray();
        Assert.That(actualRows.Count, Is.EqualTo(expectedRows.Count), $"{label}: row count");

        var expectedColumns = expectedRows.Count > 0 ? expectedRows[0].Keys.ToList() : new List<string>();
        var actualColumns = actualRows.Count > 0 ? actualRows[0].Keys.ToList() : new List<string>();
        var columns = actualColumns
            .Where(column => expectedColumns.Any(expected => string.Equals(expected, column, StringComparison.OrdinalIgnoreCase)))
            .Where(column => !ignored.Any(item => string.Equals(item, column, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expectedSorted = expectedRows.OrderBy(row => SortKey(row, columns), StringComparer.Ordinal).ToList();
        var actualSorted = actualRows.OrderBy(row => SortKey(row, columns), StringComparer.Ordinal).ToList();
        var differences = new List<string>();
        for (var row = 0; row < expectedSorted.Count; row++)
        {
            foreach (var column in columns)
            {
                if (!ValuesMatch(expectedSorted[row][column], actualSorted[row][column]))
                    differences.Add($"row {row + 1} [{column}]: expected={expectedSorted[row][column] ?? "NULL"} actual={actualSorted[row][column] ?? "NULL"}");
            }
        }

        Assert.That(differences, Is.Empty, $"{label}: {string.Join("; ", differences)}");
    }

    private static string SortKey(Dictionary<string, object?> row, IEnumerable<string> columns)
        => string.Join("|", columns.Select(column => row[column] is { } value && IsNumeric(value)
            ? Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture)
            : row[column]?.ToString() ?? string.Empty));

    private List<Dictionary<string, object?>> ExecuteOQuery(string report)
    {
        var (domain, text) = Reports[report];
        var query = QueryDefinitionOQueryParser.Parse(text);
        return _engines[domain].Query(query).ToList().Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private List<Dictionary<string, object?>> ExecuteSql(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(row);
        }

        return rows;
    }

    private static bool ValuesMatch(object? expected, object? actual)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null;

        if (IsNumeric(expected) && IsNumeric(actual))
            return Math.Abs(Convert.ToDecimal(expected, CultureInfo.InvariantCulture) - Convert.ToDecimal(actual, CultureInfo.InvariantCulture)) <= 0.01m;

        return string.Equals(expected.ToString(), actual.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumeric(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static Dictionary<string, string> LoadReferenceSql()
    {
        var markdown = Path.Combine(
            GetRepositoryRoot(), "oquery", "data-storage", "scripts", "northstar-samples-docs",
            "northstar-demo-queries.sqlserver.md");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(markdown);
        string? domainHeading = null;
        var reportNumber = (string?)null;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                domainHeading = line[3..].Trim();
                reportNumber = null;
                continue;
            }

            var heading = Regex.Match(line, @"^### (\d+)\. ");
            if (heading.Success)
            {
                reportNumber = heading.Groups[1].Value;
                continue;
            }

            if (reportNumber is null || domainHeading is null || !DomainHeadings.ContainsKey(domainHeading))
                continue;

            if (line.Trim() != "```sql")
                continue;

            var sqlLines = new List<string>();
            for (index++; index < lines.Length && lines[index].Trim() != "```"; index++)
                sqlLines.Add(lines[index].StartsWith("    ", StringComparison.Ordinal) ? lines[index][4..] : lines[index]);

            result[$"{DomainPrefix(domainHeading)} {reportNumber}"] = QualifyTables(string.Join(Environment.NewLine, sqlLines));
            reportNumber = null;
        }

        return result;
    }

    private static readonly IReadOnlyDictionary<string, string> SeedTables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Orders"] = "[sa].[Orders]",
        ["OrderItems"] = "[sa].[OrderItems]",
        ["Products"] = "[sa].[Products]",
        ["Users"] = "[main].[Users]",
        ["Employees"] = "[main].[Employees]",
        ["Locations"] = "[main].[Locations]",
        ["Departments"] = "[hr].[Departments]",
        ["LeaveRequests"] = "[hr].[LeaveRequests]",
        ["PerformanceReviews"] = "[hr].[PerformanceReviews]",
        ["Diagnoses"] = "[he].[Diagnoses]",
        ["Visits"] = "[he].[Visits]",
        ["LabResults"] = "[he].[LabResults]",
        ["InsurancePlans"] = "[in].[InsurancePlans]",
        ["Policies"] = "[in].[Policies]",
        ["PremiumPayments"] = "[in].[PremiumPayments]",
        ["Claims"] = "[in].[Claims]",
    };

    // The markdown SQL uses bare table names; the seeded database keeps them in schemas.
    private static string QualifyTables(string sql)
        => Regex.Replace(
            Regex.Replace(sql, @"(?i)\bAS Plan\b", "AS [Plan]"),
            @"(?i)\b(FROM|JOIN)\s+(\w+)\b",
            match => SeedTables.TryGetValue(match.Groups[2].Value, out var table)
                ? $"{match.Groups[1].Value} {table}"
                : match.Value);

    private static string DomainPrefix(string domainHeading) => domainHeading switch
    {
        "Sales" => "Sales",
        "Health care" => "Healthcare",
        "Insurance" => "Insurance",
        "Human resources" => "HR",
        _ => domainHeading,
    };

    private static IReadOnlyCollection<OQuery.Entity> LoadDomain(string path)
    {
        var domain = JsonSerializer.Deserialize<DomainFile>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Domain file '{path}' is empty.");

        var entities = domain.Entities.ToDictionary(
            entity => entity.Name,
            entity => new OQuery.Entity(
                entity.Name,
                entity.Fields.Select(field => new Field(field.Name, field.DbColumn, field.DataType, field.IsRawSql)).ToList(),
                entity.DbSchema,
                entity.DbTable ?? entity.RawSql,
                [],
                entity.IsRawSql),
            StringComparer.OrdinalIgnoreCase);

        foreach (var relation in domain.Relations)
        {
            if (!entities.TryGetValue(relation.SourceEntity, out var source)
                || !entities.TryGetValue(relation.TargetEntity, out var target))
                continue;

            source.Relations?.Add(new Relation(
                relation.SourceRelationName,
                relation.TargetEntity,
                RelationType.Lookup,
                relation.SourceFields.Zip(relation.TargetFields, (sourceField, targetField) => new RelationItem(sourceField, targetField)).ToList()));
            target.Relations?.Add(new Relation(
                relation.TargetRelationName,
                relation.SourceEntity,
                RelationType.Collection,
                relation.TargetFields.Zip(relation.SourceFields, (targetField, sourceField) => new RelationItem(targetField, sourceField)).ToList()));
        }

        return entities.Values.ToList();
    }

    private static string GetDomainsRoot()
        => Path.Combine(GetRepositoryRoot(), "oquery", "data-storage", "northstar-demo", "domains");

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "oquery", "data-storage")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find oquery/data-storage above the test output folder.");
    }

    private sealed class DomainFile
    {
        public string Id { get; set; } = string.Empty;
        public List<EntityFile> Entities { get; set; } = [];
        public List<RelationFile> Relations { get; set; } = [];
    }

    private sealed class EntityFile
    {
        public string Name { get; set; } = string.Empty;
        public string? DbSchema { get; set; }
        public string? DbTable { get; set; }
        public string? RawSql { get; set; }
        public bool IsRawSql { get; set; }
        public List<FieldFile> Fields { get; set; } = [];
    }

    private sealed class FieldFile
    {
        public string Name { get; set; } = string.Empty;
        public string? DbColumn { get; set; }
        public DataType DataType { get; set; }
        public bool IsRawSql { get; set; }
    }

    private sealed class RelationFile
    {
        public string SourceEntity { get; set; } = string.Empty;
        public string SourceRelationName { get; set; } = string.Empty;
        public List<string> SourceFields { get; set; } = [];
        public string TargetEntity { get; set; } = string.Empty;
        public string TargetRelationName { get; set; } = string.Empty;
        public List<string> TargetFields { get; set; } = [];
    }
}
