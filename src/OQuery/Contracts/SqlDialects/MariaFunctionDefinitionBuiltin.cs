#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

internal sealed class MariaFunctionDefinitionBuiltin : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "Maria";

    internal override IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
        base.CreateAllFunctions().Append(GetDate()).Append(Round());

    private SqlFunctionDefintion GetDate() =>
        Create("GETDATE", [], DataType.Datetime, "Returns the current date and time.", "CURRENT_TIMESTAMP");

    private SqlFunctionDefintion Round() =>
        Create("ROUND", [new("value", DataType.Number), new("digits", DataType.Number)],
            DataType.Number, "Rounds a number to the specified number of decimal places.",
            "ROUND({1}, {2})");

    protected override SqlFunctionDefintion Day() => ExtractDatePart("DAY");
    protected override SqlFunctionDefintion Month() => ExtractDatePart("MONTH");
    protected override SqlFunctionDefintion Year() => ExtractDatePart("YEAR");

    protected override SqlFunctionDefintion DateSub() => DateDifference(
        "DATESUB",
        "DATEDIFF({2}, {1})",
        "Returns the number of elapsed whole days from startDate to endDate.");

    protected override SqlFunctionDefintion MonthSub() => DateDifference(
        "MONTHSUB",
        "TIMESTAMPDIFF(MONTH, {1}, {2})",
        "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion YearSub() => DateDifference(
        "YEARSUB",
        "TIMESTAMPDIFF(YEAR, {1}, {2})",
        "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion DateAdd() => DateAdjustment(
        "DATEADD",
        "DATE_ADD({1}, INTERVAL {2} DAY)",
        "Adds the specified number of days to a date or date-time.");

    protected override SqlFunctionDefintion MonthAdd() => DateAdjustment(
        "MONTHADD",
        "DATE_ADD({1}, INTERVAL {2} MONTH)",
        "Adds the specified number of months to a date or date-time.");

    protected override SqlFunctionDefintion YearAdd() => DateAdjustment(
        "YEARADD",
        "DATE_ADD({1}, INTERVAL {2} YEAR)",
        "Adds the specified number of years to a date or date-time.");

    protected override SqlFunctionDefintion MonthKey() =>
        Create("MONTHKEY", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "DATE_FORMAT({1}, '%Y-%m')");

    protected override SqlFunctionDefintion AgeOn() =>
        Create("AGEON",
            [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number,
            "Returns completed years between a birth date and an as-of date.",
            "TIMESTAMPDIFF(YEAR, {1}, {2})");

    protected override SqlFunctionDefintion ElapsedYears() =>
        Create("ELAPSEDYEARS",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "(DATEDIFF({2}, {1}) / 365.2425)");

    protected override SqlFunctionDefintion Lag() =>
        Create("LAG",
            [new("expression", DataType.Number), new("offset", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns the previous row's value in ascending order.",
            "LAG({1}, {2}) OVER (ORDER BY {3} ASC)");

    private SqlFunctionDefintion ExtractDatePart(string part) =>
        Create(part, [new("extractDate", DataType.Datetime)], DataType.Number,
            $"Extracts the {part.ToLowerInvariant()} part from a date.",
            $"{part}({{1}})");

    private SqlFunctionDefintion DateDifference(string name, string template, string description) =>
        Create(name, [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, template);

    private SqlFunctionDefintion DateAdjustment(string name, string template, string description) =>
        Create(name, [new("value", DataType.Datetime), new("amount", DataType.Number)],
            DataType.Datetime, description, template);
}
