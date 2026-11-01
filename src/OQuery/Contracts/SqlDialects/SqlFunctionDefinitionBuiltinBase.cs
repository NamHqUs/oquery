#pragma warning disable IDE0130
using static OQuery.SqlFunctionDefintion;

namespace OQuery;
#pragma warning restore IDE0130

internal abstract class SqlFunctionDefinitionBuiltinBase
{
    protected abstract string Dialect { get; }

    internal virtual IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
    [
        Count(),
        Sum(),
        Average(),
        Maximum(),
        Minimum(),
        NullIf(),
        Day(),
        Month(),
        Year(),
        DateSub(),
        DateAdd(),
        MonthSub(),
        MonthAdd(),
        YearSub(),
        YearAdd(),
        CountDistinct(),
        CountIf(),
        AverageIf(),
        Coalesce(),
        MedicalLossRatio(),
        Concat(),
        MonthKey(),
        AgeOn(),
        ElapsedYears(),
        Difference(),
        Lag(),
        WindowSum(),
        Rank(),
        RowNumber()
    ];

    protected virtual SqlFunctionDefintion Count() =>
        Create("COUNT", [new("value", DataType.Number, "*")], DataType.Number,
            "Counts rows or non-null values.", "COUNT({1})", isAggregate: true);

    protected virtual SqlFunctionDefintion Sum() =>
        Create("SUM", [new("expression", DataType.Number)], DataType.Number,
            "Returns the sum of numeric values.", "SUM({1})", isAggregate: true);

    protected virtual SqlFunctionDefintion Average() =>
        Create("AVG", [new("expression", DataType.Number)], DataType.Number,
            "Returns the average of numeric values.", "AVG({1})", isAggregate: true);

    protected virtual SqlFunctionDefintion Maximum() =>
        Create("MAX", [new("expression", DataType.Number)], DataType.Number,
            "Returns the maximum value.", "MAX({1})", isAggregate: true);

    protected virtual SqlFunctionDefintion Minimum() =>
        Create("MIN", [new("expression", DataType.Number)], DataType.Number,
            "Returns the minimum value.", "MIN({1})", isAggregate: true);

    protected virtual SqlFunctionDefintion NullIf() =>
        Create("NULLIF", [new("value", DataType.Number), new("otherValue", DataType.Number)],
            DataType.Number, "Returns null when the two values are equal; otherwise returns the first value.",
            "NULLIF({1}, {2})");

    protected virtual SqlFunctionDefintion Day() =>
        Create("DAY", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the day part from a date.", "DAY({1})");

    protected virtual SqlFunctionDefintion Month() =>
        Create("MONTH", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the month part from a date.", "MONTH({1})");

    protected virtual SqlFunctionDefintion Year() =>
        Create("YEAR", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the year part from a date.", "YEAR({1})");

    protected virtual SqlFunctionDefintion DateSub() =>
        CreateDateDifference("DATESUB", "day",
            "Returns the number of elapsed whole days from startDate to endDate.");

    protected virtual SqlFunctionDefintion DateAdd() =>
        CreateDateAdjustment("DATEADD", "day",
            "Adds the specified number of days to a date or date-time.");

    protected virtual SqlFunctionDefintion MonthSub() =>
        CreateDateDifference("MONTHSUB", "month",
            "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected virtual SqlFunctionDefintion MonthAdd() =>
        CreateDateAdjustment("MONTHADD", "month",
            "Adds the specified number of months to a date or date-time.");

    protected virtual SqlFunctionDefintion YearSub() =>
        CreateDateDifference("YEARSUB", "year",
            "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected virtual SqlFunctionDefintion YearAdd() =>
        CreateDateAdjustment("YEARADD", "year",
            "Adds the specified number of years to a date or date-time.");

    protected virtual SqlFunctionDefintion CountDistinct() =>
        Create("COUNTDISTINCT", [new("expression", DataType.Number)], DataType.Number,
            "Counts distinct non-null values in a related collection.",
            "COUNT(DISTINCT {1})", isAggregate: true);

    protected virtual SqlFunctionDefintion CountIf() =>
        Create("COUNTIF", [new("predicate", DataType.Boolean)], DataType.Number,
            "Counts related rows matching a predicate.",
            "COUNT(CASE WHEN {1} THEN 1 END)", isAggregate: true);

    protected virtual SqlFunctionDefintion AverageIf() =>
        Create("AVGIF",
            [new("expression", DataType.Number), new("predicate", DataType.Boolean)],
            DataType.Number, "Averages related values matching a predicate.",
            "AVG(CASE WHEN {2} THEN {1} END)", isAggregate: true);

    protected virtual SqlFunctionDefintion Coalesce() =>
        Create("COALESCE",
            [new("value", DataType.Number), new("fallback", DataType.Number)],
            DataType.Number, "Returns the first non-null numeric value.",
            "COALESCE({1}, {2})");

    protected virtual SqlFunctionDefintion MedicalLossRatio() =>
        Create("MEDICALLOSSRATIO",
            [new("claimsPaid", DataType.Number), new("premiumRevenue", DataType.Number)],
            DataType.Number, "Calculates paid claims as a percentage of premium revenue.",
            "ROUND(100.0 * COALESCE(MAX({1}), 0) / NULLIF(MAX({2}), 0), 2)",
            isAggregate: true);

    protected virtual SqlFunctionDefintion Concat() =>
        Create("CONCAT",
            [new("first", DataType.String), new("separator", DataType.String), new("last", DataType.String)],
            DataType.String, "Concatenates three text values.", "CONCAT({1}, {2}, {3})");

    protected virtual SqlFunctionDefintion MonthKey() =>
        Create("MONTHKEY", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "CONVERT(char(7), {1}, 126)");

    protected virtual SqlFunctionDefintion AgeOn() =>
        Create("AGEON",
            [new("birthDate", DataType.Datetime), new("asOfDate", DataType.Datetime)],
            DataType.Number,
            "Returns completed years between a start date and an as-of date.",
            "(DATEDIFF(year, {1}, {2}) - CASE WHEN DATEPART(month, {2}) * 100 + DATEPART(day, {2}) < DATEPART(month, {1}) * 100 + DATEPART(day, {1}) THEN 1 ELSE 0 END)");

    protected virtual SqlFunctionDefintion ElapsedYears() =>
        Create("ELAPSEDYEARS",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "(DATEDIFF(day, {1}, {2}) / 365.2425)");

    protected virtual SqlFunctionDefintion Difference() =>
        Create("DIFFERENCE",
            [new("left", DataType.Number), new("right", DataType.Number)],
            DataType.Number, "Returns the difference between two numeric values.", "({1} - {2})");

    protected virtual SqlFunctionDefintion Lag() =>
        Create("LAG",
            [new("expression", DataType.Number), new("offset", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns the previous row's value in ascending order.",
            "LAG({1}, {2}) OVER (ORDER BY {3} ASC)");

    protected virtual SqlFunctionDefintion WindowSum() =>
        Create("WINDOWSUM",
            [new("expression", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns a running total in ascending order.",
            "SUM({1}) OVER (ORDER BY {2} ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)");

    protected virtual SqlFunctionDefintion Rank() =>
        Create("RANK",
            [new("partitionBy", DataType.Number), new("orderBy", DataType.Number)],
            DataType.Number, "Ranks rows within each partition in descending order.",
            "RANK() OVER (PARTITION BY {1} ORDER BY {2} DESC)", isAggregate: true);

    protected virtual SqlFunctionDefintion RowNumber() =>
        Create("ROWNUMBER",
            [new("partitionBy", DataType.Number), new("orderBy", DataType.Datetime)],
            DataType.Number, "Numbers rows independently within each partition in descending order.",
            "ROW_NUMBER() OVER (PARTITION BY {1} ORDER BY {2} DESC)", isAggregate: true);

    protected SqlFunctionDefintion Create(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string description,
        string sqlTemplate,
        bool isAggregate = false)
        => new(name, arguments, resultType, description,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Dialect] = sqlTemplate
            },
            isAggregate);

    private SqlFunctionDefintion CreateDateDifference(
        string name,
        string datePart,
        string description)
        => Create(name,
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, description, $"DATEDIFF({datePart}, {{1}}, {{2}})");

    private SqlFunctionDefintion CreateDateAdjustment(
        string name,
        string datePart,
        string description)
        => Create(name,
            [new("value", DataType.Datetime), new("amount", DataType.Number)],
            DataType.Datetime, description, $"DATEADD({datePart}, {{2}}, {{1}})");
}
