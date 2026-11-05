#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

internal sealed class SapHanaFunctionDefinitionBuiltin : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "SapHana";

    internal override IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
        base.CreateAllFunctions().Append(GetDate()).Append(Round());

    private SqlFunctionDefintion GetDate() =>
        Create("GetDate", [], DataType.Datetime, "Returns the current date and time.", "CURRENT_TIMESTAMP");

    private SqlFunctionDefintion Round() =>
        Create("Round", [new("value", DataType.Number), new("digits", DataType.Number)],
            DataType.Number, "Rounds a number to the specified number of decimal places.",
        "ROUND({1}, CAST({2} AS INTEGER))");

    protected override SqlFunctionDefintion Concat() =>
        Create("Concat",
            [new("first", DataType.String), new("separator", DataType.String), new("last", DataType.String)],
            DataType.String, "Concatenates three text values.", "({1} || {2} || {3})");

    protected override SqlFunctionDefintion Day() => CreateDatePart("DAYOFMONTH");
    protected override SqlFunctionDefintion Month() => CreateDatePart("MONTH");
    protected override SqlFunctionDefintion Year() => CreateDatePart("YEAR");

    protected override SqlFunctionDefintion DateSub() => DateDifference(
        "DATESUB", "DAYS_BETWEEN({1}, {2})",
        "Returns the number of elapsed whole days from startDate to endDate.");

    protected override SqlFunctionDefintion MonthSub() => DateDifference(
        "MONTHSUB", "((YEAR({2}) - YEAR({1})) * 12 + MONTH({2}) - MONTH({1}))",
        "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion YearSub() => DateDifference(
        "YEARSUB", "(YEAR({2}) - YEAR({1}))",
        "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion DateAdd() => DateAdjustment(
        "DATEADD", "ADD_DAYS({1}, {2})",
        "Adds the specified number of days to a date or date-time.");

    protected override SqlFunctionDefintion MonthAdd() => DateAdjustment(
        "MONTHADD", "ADD_MONTHS({1}, {2})",
        "Adds the specified number of months to a date or date-time.");

    protected override SqlFunctionDefintion YearAdd() => DateAdjustment(
        "YEARADD", "ADD_YEARS({1}, {2})",
        "Adds the specified number of years to a date or date-time.");

    protected override SqlFunctionDefintion MonthKey() =>
        Create("MonthKey", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "TO_VARCHAR({1}, 'YYYY-MM')");

    protected override SqlFunctionDefintion Coalesce() =>
        Create("Coalesce",
            [new("value", DataType.Number), new("fallback", DataType.Number)],
            DataType.Number, "Returns the first non-null numeric value.",
            "COALESCE(CAST({1} AS DECIMAL(34, 10)), 0)");

    protected override SqlFunctionDefintion AgeOn() =>
        Create("AgeOn",
            [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number, "Returns completed years between a birth date and an as-of date.",
            "YEARS_BETWEEN({1}, {2})");

    protected override SqlFunctionDefintion ElapsedYears() =>
        Create("ElapsedYears",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "(DAYS_BETWEEN({1}, {2}) / 365.2425)");

    protected override SqlFunctionDefintion Lag() =>
        Create("Lag",
            [new("expression", DataType.Number), new("offset", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns the previous row's value in ascending order.",
            "LAG({1}, {2}) OVER (ORDER BY {3} ASC)");

    private SqlFunctionDefintion CreateDatePart(string functionName) =>
        Create(functionName, [new("extractDate", DataType.Datetime)], DataType.Number,
            $"Extracts the {functionName.ToLowerInvariant()} part from a date.",
            $"{functionName}({{1}})");

    private SqlFunctionDefintion DateDifference(string name, string template, string description) =>
        Create(name, [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, template);

    private SqlFunctionDefintion DateAdjustment(string name, string template, string description) =>
        Create(name, [new("value", DataType.Datetime), new("amount", DataType.Number)],
            DataType.Datetime, description, template);
}
