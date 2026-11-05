#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

internal sealed class FirebirdFunctionDefinitionBuiltin : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "Firebird";

    internal override IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
        base.CreateAllFunctions().Append(GetDate()).Append(Round());

    private SqlFunctionDefintion GetDate() =>
        Create("GetDate", [], DataType.Datetime, "Returns the current date and time.", "CURRENT_TIMESTAMP");

    private SqlFunctionDefintion Round() =>
        Create("Round", [new("value", DataType.Number), new("digits", DataType.Number)],
            DataType.Number, "Rounds a number to the specified number of decimal places.",
            "ROUND({1}, 2)");

    protected override SqlFunctionDefintion Day() => ExtractDatePart("DAY");
    protected override SqlFunctionDefintion Month() => ExtractDatePart("MONTH");
    protected override SqlFunctionDefintion Year() => ExtractDatePart("YEAR");

    protected override SqlFunctionDefintion DateSub() => DateDifference("DateSub",
        "TRUNC(CAST({2} AS DATE) - CAST({1} AS DATE))",
        "Returns the number of elapsed whole days from startDate to endDate.");

    protected override SqlFunctionDefintion MonthSub() => DateDifference("MonthSub",
        "((EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1})) * 12 + EXTRACT(MONTH FROM {2}) - EXTRACT(MONTH FROM {1}))",
        "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion YearSub() => DateDifference("YearSub",
        "(EXTRACT(YEAR FROM {2}) - EXTRACT(YEAR FROM {1}))",
        "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion DateAdd() => DateAdjustment("DateAdd", "day",
        "({1} + ({2} * INTERVAL '1 day'))",
        "Adds the specified number of days to a date or date-time.");

    protected override SqlFunctionDefintion MonthAdd() => DateAdjustment("MonthAdd", "month",
        "({1} + ({2} * INTERVAL '1 month'))",
        "Adds the specified number of months to a date or date-time.");

    protected override SqlFunctionDefintion YearAdd() => DateAdjustment("YearAdd", "year",
        "({1} + ({2} * INTERVAL '1 year'))",
        "Adds the specified number of years to a date or date-time.");

    protected override SqlFunctionDefintion Coalesce() =>
        Create("Coalesce", [new("value", DataType.Number), new("fallback", DataType.Number)],
            DataType.Number, "Returns the first non-null numeric value.", "COALESCE({1}, 0)");

    protected override SqlFunctionDefintion MedicalLossRatio() =>
        Create("MedicalLossRatio",
            [new("claimsPaid", DataType.Number), new("premiumRevenue", DataType.Number)],
            DataType.Number, "Calculates paid claims as a percentage of premium revenue.",
            "ROUND(100 * COALESCE(MAX({1}), 0) / NULLIF(MAX({2}), 0), 2)", isAggregate: true);

    protected override SqlFunctionDefintion Concat() =>
        Create("Concat",
            [new("first", DataType.String), new("separator", DataType.String), new("last", DataType.String)],
            DataType.String, "Concatenates three text values.", "({1} || ' ' || {3})");

    protected override SqlFunctionDefintion MonthKey() =>
        Create("MonthKey", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "SUBSTRING(CAST({1} AS VARCHAR(10)) FROM 1 FOR 7)");

    protected override SqlFunctionDefintion AgeOn() =>
        Create("AgeOn", [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number, "Returns completed years between a start date and an as-of date.",
            "(EXTRACT(YEAR FROM CAST({2} AS DATE)) - EXTRACT(YEAR FROM CAST({1} AS DATE)) - CASE WHEN EXTRACT(MONTH FROM CAST({2} AS DATE)) * 100 + EXTRACT(DAY FROM CAST({2} AS DATE)) < EXTRACT(MONTH FROM CAST({1} AS DATE)) * 100 + EXTRACT(DAY FROM CAST({1} AS DATE)) THEN 1 ELSE 0 END)");

    protected override SqlFunctionDefintion ElapsedYears() =>
        Create("ElapsedYears", [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "(EXTRACT(EPOCH FROM ({2} - {1})) / 31556952.0)");

    protected override SqlFunctionDefintion Lag() =>
        Create("Lag", [new("expression", DataType.Number), new("offset", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns the previous row's value in ascending order.",
            "LAG({1}, CAST({2} AS INTEGER)) OVER (ORDER BY {3} ASC)");

    private SqlFunctionDefintion ExtractDatePart(string part) =>
        Create(part, [new("extractDate", DataType.Datetime)], DataType.Number,
            $"Extracts the {part.ToLowerInvariant()} part from a date.",
            $"CAST(EXTRACT({part} FROM {{1}}) AS INTEGER)");

    private SqlFunctionDefintion DateDifference(string name, string template, string description) =>
        Create(name, [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, template);

    private SqlFunctionDefintion DateAdjustment(string name, string datePart, string template, string description) =>
        Create(name, [new("value", DataType.Datetime), new("amount", DataType.Number)],
            DataType.Datetime, description, template);
}
