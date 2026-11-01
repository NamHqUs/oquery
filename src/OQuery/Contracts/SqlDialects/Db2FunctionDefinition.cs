#pragma warning disable IDE0130
using static OQuery.SqlFunctionDefintion;

namespace OQuery;
#pragma warning restore IDE0130

internal sealed class Db2FunctionDefinition : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "Db2";

    internal override IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
        base.CreateAllFunctions().Append(Round());

    protected override SqlFunctionDefintion MedicalLossRatio() =>
        Create("MEDICALLOSSRATIO",
            [new("claimsPaid", DataType.Number), new("premiumRevenue", DataType.Number)],
            DataType.Number, "Calculates paid claims as a percentage of premium revenue.",
            "ROUND(CAST(100.0 AS DECIMAL(31, 15)) * COALESCE(MAX({1}), 0) " +
            "/ NULLIF(MAX({2}), 0), 2)", isAggregate: true);

    private SqlFunctionDefintion Round() =>
        Create("ROUND", [new("value", DataType.Number), new("digits", DataType.Number)],
            DataType.Number, "Rounds a number to the specified number of decimal places.",
            "ROUND({1}, {2})");

    protected override SqlFunctionDefintion Concat() =>
        Create("CONCAT",
            [new("first", DataType.String), new("separator", DataType.String), new("last", DataType.String)],
            DataType.String, "Concatenates three text values.", "({1} || {2} || {3})");

    protected override SqlFunctionDefintion DateSub() => DateDifference(
        "DATESUB", "DAYS(CAST({2} AS DATE)) - DAYS(CAST({1} AS DATE))",
        "Returns the number of elapsed whole days from startDate to endDate.");

    protected override SqlFunctionDefintion MonthSub() => DateDifference(
        "MONTHSUB", "((YEAR({2}) - YEAR({1})) * 12 + MONTH({2}) - MONTH({1}))",
        "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion YearSub() => DateDifference(
        "YEARSUB", "(YEAR({2}) - YEAR({1}))",
        "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion DateAdd() => DateAdjustment(
        "DATEADD", "{1} + {2} DAYS",
        "Adds the specified number of days to a date or date-time.");

    protected override SqlFunctionDefintion MonthAdd() => DateAdjustment(
        "MONTHADD", "{1} + {2} MONTHS",
        "Adds the specified number of months to a date or date-time.");

    protected override SqlFunctionDefintion YearAdd() => DateAdjustment(
        "YEARADD", "{1} + {2} YEARS",
        "Adds the specified number of years to a date or date-time.");

    protected override SqlFunctionDefintion MonthKey() =>
        Create("MONTHKEY", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "VARCHAR_FORMAT({1}, 'YYYY-MM')");

    protected override SqlFunctionDefintion AgeOn() =>
        Create("AGEON",
            [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number, "Returns completed years between a birth date and an as-of date.",
            "(YEAR(CAST({2} AS DATE)) - YEAR(CAST({1} AS DATE)) - " +
            "CASE WHEN MONTH(CAST({2} AS DATE)) * 100 + DAY(CAST({2} AS DATE)) < " +
            "MONTH(CAST({1} AS DATE)) * 100 + DAY(CAST({1} AS DATE)) THEN 1 ELSE 0 END)");

    protected override SqlFunctionDefintion ElapsedYears() =>
        Create("ELAPSEDYEARS",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "(DECIMAL(DAYS(CAST({2} AS DATE)) - DAYS(CAST({1} AS DATE)), 18, 6) / 365.2425)");

    protected override SqlFunctionDefintion Lag() =>
        Create("LAG",
            [new("expression", DataType.Number), new("offset", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns the previous row's value in ascending order.",
            "LAG({1}, 1) OVER (ORDER BY {3} ASC)");

    private SqlFunctionDefintion DateDifference(string name, string template, string description) =>
        Create(name, [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, template);

    private SqlFunctionDefintion DateAdjustment(string name, string template, string description) =>
        Create(name, [new("value", DataType.Datetime), new("amount", DataType.Number)],
            DataType.Datetime, description, template);
}
