#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

internal sealed class OracleFunctionDefinitionBuiltin : SqlFunctionDefinitionBuiltinBase
{
    protected override string Dialect => "Oracle";

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

    protected override SqlFunctionDefintion DateSub() => DateDifference("DATESUB", "day",
        "TRUNC(CAST({2} AS DATE) - CAST({1} AS DATE))",
        "Returns the number of elapsed whole days from startDate to endDate.");

    protected override SqlFunctionDefintion MonthSub() => DateDifference("MONTHSUB", "month",
        "((EXTRACT(YEAR FROM CAST({2} AS DATE)) - EXTRACT(YEAR FROM CAST({1} AS DATE))) * 12 + EXTRACT(MONTH FROM CAST({2} AS DATE)) - EXTRACT(MONTH FROM CAST({1} AS DATE)))",
        "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion YearSub() => DateDifference("YEARSUB", "year",
        "(EXTRACT(YEAR FROM CAST({2} AS DATE)) - EXTRACT(YEAR FROM CAST({1} AS DATE)))",
        "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected override SqlFunctionDefintion MonthKey() =>
        Create("MONTHKEY", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "TO_CHAR(CAST({1} AS DATE), 'YYYY-MM')");

    protected override SqlFunctionDefintion AgeOn() =>
        Create("AGEON", [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number, "Returns completed years between a start date and an as-of date.",
            "(EXTRACT(YEAR FROM CAST({2} AS DATE)) - EXTRACT(YEAR FROM CAST({1} AS DATE)) - CASE WHEN EXTRACT(MONTH FROM CAST({2} AS DATE)) * 100 + EXTRACT(DAY FROM CAST({2} AS DATE)) < EXTRACT(MONTH FROM CAST({1} AS DATE)) * 100 + EXTRACT(DAY FROM CAST({1} AS DATE)) THEN 1 ELSE 0 END)");

    private SqlFunctionDefintion ExtractDatePart(string part) =>
        Create(part, [new("extractDate", DataType.Datetime)], DataType.Number,
            $"Extracts the {part.ToLowerInvariant()} part from a date.",
            $"EXTRACT({part} FROM {{1}})");

    private SqlFunctionDefintion DateDifference(string name, string datePart, string template, string description) =>
        Create(name, [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, template);
}
