#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

internal sealed class SqliteFunctionDefinitionBuiltin : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "Sqlite";

    internal override IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
        base.CreateAllFunctions().Append(GetDate()).Append(Round()).Append(DateFormat());

    private SqlFunctionDefintion GetDate() =>
        Create("GETDATE", [], DataType.Datetime, "Returns the current date and time.",
            "datetime('now', 'localtime')");

    private SqlFunctionDefintion Round() =>
        Create("ROUND", [new("value", DataType.Number), new("digits", DataType.Number)],
            DataType.Number, "Rounds a number to the specified number of decimal places.",
            "ROUND({1}, {2})");

    private SqlFunctionDefintion DateFormat() =>
        Create("DATEFORMAT", [new("value", DataType.Datetime), new("format", DataType.String)],
            DataType.String,
            "Formats a date as year-month (%Y-%m) or weekday number (%w) on SQLite.",
            "STRFTIME({2}, {1})");

    protected override SqlFunctionDefintion Day() =>
        Create("DAY", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the day part from a date.", "CAST(STRFTIME('%d', {1}) AS INTEGER)");

    protected override SqlFunctionDefintion Month() =>
        Create("MONTH", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the month part from a date.", "CAST(STRFTIME('%m', {1}) AS INTEGER)");

    protected override SqlFunctionDefintion Year() =>
        Create("YEAR", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the year part from a date.", "CAST(STRFTIME('%Y', {1}) AS INTEGER)");

    protected override SqlFunctionDefintion DateSub() => DateDifference("DATESUB", "day",
        "CAST(julianday(date({2})) - julianday(date({1})) AS INTEGER)",
        "Returns the number of elapsed whole days from startDate to endDate.");

    protected override SqlFunctionDefintion MonthSub() => DateDifference("MONTHSUB", "month",
        "((CAST(strftime('%Y', date({2})) AS INTEGER) - CAST(strftime('%Y', date({1})) AS INTEGER)) * 12 + CAST(strftime('%m', date({2})) AS INTEGER) - CAST(strftime('%m', date({1})) AS INTEGER))",
        "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion YearSub() => DateDifference("YEARSUB", "year",
        "(CAST(strftime('%Y', date({2})) AS INTEGER) - CAST(strftime('%Y', date({1})) AS INTEGER))",
        "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion DateAdd() => DateAdjustment("DATEADD", "days",
        "datetime({1}, printf('%+d days', {2}))",
        "Adds the specified number of days to a date or date-time.");

    protected override SqlFunctionDefintion MonthAdd() => DateAdjustment("MONTHADD", "months",
        "datetime({1}, printf('%+d months', {2}))",
        "Adds the specified number of months to a date or date-time.");

    protected override SqlFunctionDefintion YearAdd() => DateAdjustment("YEARADD", "years",
        "datetime({1}, printf('%+d years', {2}))",
        "Adds the specified number of years to a date or date-time.");

    protected override SqlFunctionDefintion MonthKey() =>
        Create("MONTHKEY", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "STRFTIME('%Y-%m', {1})");

    protected override SqlFunctionDefintion AgeOn() =>
        Create("AGEON", [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number, "Returns completed years between a start date and an as-of date.",
            "(CAST(STRFTIME('%Y', {2}) AS INTEGER) - CAST(STRFTIME('%Y', {1}) AS INTEGER) - CASE WHEN STRFTIME('%m%d', {2}) < STRFTIME('%m%d', {1}) THEN 1 ELSE 0 END)");

    protected override SqlFunctionDefintion ElapsedYears() =>
        Create("ELAPSEDYEARS", [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "((JULIANDAY({2}) - JULIANDAY({1})) / 365.2425)");

    private SqlFunctionDefintion DateDifference(string name, string datePart, string template, string description) =>
        Create(name, [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, template);

    private SqlFunctionDefintion DateAdjustment(string name, string unit, string template, string description) =>
        Create(name, [new("value", DataType.Datetime), new("amount", DataType.Number)],
            DataType.Datetime, description, template);
}
