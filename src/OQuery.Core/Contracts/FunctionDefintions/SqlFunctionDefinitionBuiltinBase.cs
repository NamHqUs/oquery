#pragma warning disable IDE0130
using static OQuery.SqlFunctionDefintion;

namespace OQuery;
#pragma warning restore IDE0130

public abstract class SqlFunctionDefinitionBuiltinBase
{
    protected abstract string Dialect { get; }

    protected virtual string CurrentDateTimeSql => "CURRENT_TIMESTAMP";

    public virtual IEnumerable<SqlFunctionDefintion> CreateAllFunctions() =>
    [
        Count(),
        CountOver(),
        Sum(),
        SumOver(),
        Average(),
        AverageOver(),
        Maximum(),
        MaximumOver(),
        Minimum(),
        MinimumOver(),
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
        CountIf(),
        AverageIf(),
        SumIf(),
        MinimumIf(),
        MaximumIf(),
        IfNull(),
        MedicalLossRatio(),
        Concat(),
        MonthKey(),
        YearsTo(),
        MonthsTo(),
        DaysTo(),
        YearsToNow(),
        MonthsToNow(),
        DaysToNow(),
        Lag(),
        WindowSum(),
        Rank(),
        RowNumber(),
        Any(),
        NotAny()
    ];

    protected virtual SqlFunctionDefintion Count() =>
        Create("Count", [new("value", DataType.Number, 1)], DataType.Number,
            "Counts rows or non-null values. With no argument, counts rows.", "COUNT({1})");

    protected virtual SqlFunctionDefintion CountOver() =>
        Create("CountOver",
            [new("expression", DataType.Number), new("partitionBy", DataType.Number, "*")],
            DataType.Number,
            "Counts values within a window partition without collapsing rows.",
            "COUNT({1}) OVER (PARTITION BY {2})", windowOf: "Count");

    protected virtual SqlFunctionDefintion Sum() =>
        Create("Sum", [new("expression", DataType.Number)], DataType.Number,
            "Returns the sum of numeric values.", "SUM({1})");

    protected virtual SqlFunctionDefintion SumOver() =>
        Create("SumOver",
            [new("expression", DataType.Number), new("partitionBy", DataType.Number, "*")],
            DataType.Number,
            "Returns the sum within a window partition without collapsing rows.",
            "SUM({1}) OVER (PARTITION BY {2})", windowOf: "Sum");

    protected virtual SqlFunctionDefintion Average() =>
        Create("Avg", [new("expression", DataType.Number)], DataType.Number,
            "Returns the average of numeric values.", "AVG({1})");

    protected virtual SqlFunctionDefintion AverageOver() =>
        Create("AverageOver",
            [new("expression", DataType.Number), new("partitionBy", DataType.Number, "*")],
            DataType.Number,
            "Returns the average within a window partition without collapsing rows.",
            "AVG({1}) OVER (PARTITION BY {2})", windowOf: "Avg");

    protected virtual SqlFunctionDefintion Maximum() =>
        Create("Max", [new("expression", DataType.Number)], DataType.Number,
            "Returns the maximum value.", "MAX({1})");

    protected virtual SqlFunctionDefintion MaximumOver() =>
        Create("MaxOver",
            [new("expression", DataType.Number), new("partitionBy", DataType.Number, "*")],
            DataType.Number,
            "Returns the maximum within a window partition without collapsing rows.",
            "MAX({1}) OVER (PARTITION BY {2})", windowOf: "Max");

    protected virtual SqlFunctionDefintion Minimum() =>
        Create("Min", [new("expression", DataType.Number)], DataType.Number,
            "Returns the minimum value.", "MIN({1})");

    protected virtual SqlFunctionDefintion MinimumOver() =>
        Create("MinOver",
            [new("expression", DataType.Number), new("partitionBy", DataType.Number, "*")],
            DataType.Number,
            "Returns the minimum within a window partition without collapsing rows.",
            "MIN({1}) OVER (PARTITION BY {2})", windowOf: "Min");

    protected virtual SqlFunctionDefintion NullIf() =>
        Create("NullIf", [new("value", DataType.Number), new("otherValue", DataType.Number)],
            DataType.Number, "Returns null when the two values are equal; otherwise returns the first value.",
            "NULLIF({1}, {2})");

    protected virtual SqlFunctionDefintion Day() =>
        Create("Day", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the day part from a date.", "DAY({1})");

    protected virtual SqlFunctionDefintion Month() =>
        Create("Month", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the month part from a date.", "MONTH({1})");

    protected virtual SqlFunctionDefintion Year() =>
        Create("Year", [new("extractDate", DataType.Datetime)], DataType.Number,
            "Extracts the year part from a date.", "YEAR({1})");

    protected virtual SqlFunctionDefintion DateSub() =>
        CreateDateDifference("DateSub", "day",
            "Returns the number of elapsed whole days from startDate to endDate.");

    protected virtual SqlFunctionDefintion DateAdd() =>
        CreateDateAdjustment("DateAdd", "day",
            "Adds the specified number of days to a date or date-time.");

    protected virtual SqlFunctionDefintion MonthSub() =>
        CreateDateDifference("MonthSub", "month",
            "Returns the number of calendar-month boundaries from startDate to endDate.");

    protected virtual SqlFunctionDefintion MonthAdd() =>
        CreateDateAdjustment("MonthAdd", "month",
            "Adds the specified number of months to a date or date-time.");

    protected virtual SqlFunctionDefintion YearSub() =>
        CreateDateDifference("YearSub", "year",
            "Returns the number of calendar-year boundaries from startDate to endDate.");

    protected virtual SqlFunctionDefintion YearAdd() =>
        CreateDateAdjustment("YearAdd", "year",
            "Adds the specified number of years to a date or date-time.");

    protected virtual SqlFunctionDefintion CountIf() =>
        Create("CountIf",
            [new("expression", DataType.Number, 1), new("predicate", DataType.Boolean)],
            DataType.Number,
            "Counts related rows matching a predicate, or non-null expression values on matching rows.",
            "COUNT(CASE WHEN {2} THEN {1} END)", bindArgumentsFromEnd: true);

    protected virtual SqlFunctionDefintion AverageIf() =>
        Create("AvgIf",
            [new("expression", DataType.Number), new("predicate", DataType.Boolean)],
            DataType.Number, "Averages related values matching a predicate.",
            "AVG(CASE WHEN {2} THEN {1} END)");

    protected virtual SqlFunctionDefintion SumIf() =>
        Create("SumIf",
            [new("expression", DataType.Number), new("predicate", DataType.Boolean)],
            DataType.Number, "Sums related values matching a predicate.",
            "SUM(CASE WHEN {2} THEN {1} END)");

    protected virtual SqlFunctionDefintion MinimumIf() =>
        Create("MinIf",
            [new("expression", DataType.Number), new("predicate", DataType.Boolean)],
            DataType.Number, "Returns the minimum related value matching a predicate.",
            "MIN(CASE WHEN {2} THEN {1} END)");

    protected virtual SqlFunctionDefintion MaximumIf() =>
        Create("MaxIf",
            [new("expression", DataType.Number), new("predicate", DataType.Boolean)],
            DataType.Number, "Returns the maximum related value matching a predicate.",
            "MAX(CASE WHEN {2} THEN {1} END)");

    protected virtual SqlFunctionDefintion IfNull() =>
        Create("IfNull",
            [new("value", DataType.Number), new("fallback", DataType.Number)],
            DataType.Number, "Returns the first non-null numeric value.",
            "COALESCE({1}, {2})");

    protected virtual SqlFunctionDefintion MedicalLossRatio() =>
        Create("MedicalLossRatio",
            [new("claimsPaid", DataType.Number), new("premiumRevenue", DataType.Number)],
            DataType.Number, "Calculates paid claims as a percentage of premium revenue.",
            "ROUND(100.0 * COALESCE(MAX({1}), 0) / NULLIF(MAX({2}), 0), 2)");

    protected virtual SqlFunctionDefintion Concat() =>
        Create("Concat",
            [new("first", DataType.String), new("separator", DataType.String), new("last", DataType.String)],
            DataType.String, "Concatenates three text values.", "CONCAT({1}, {2}, {3})");

    protected virtual SqlFunctionDefintion MonthKey() =>
        Create("MonthKey", [new("value", DataType.Datetime)], DataType.String,
            "Formats a date as a year-month key.", "CONVERT(char(7), {1}, 126)");

    protected virtual SqlFunctionDefintion YearsTo() =>
        Create("YearsTo",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number,
            "Returns completed years between startDate and endDate.",
            "(DATEDIFF(year, {1}, {2}) - CASE WHEN DATEPART(month, {2}) * 100 + DATEPART(day, {2}) < DATEPART(month, {1}) * 100 + DATEPART(day, {1}) THEN 1 ELSE 0 END)");

    protected virtual SqlFunctionDefintion ElapsedYears() =>
        Create("ElapsedYears",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns fractional years elapsed between two dates.",
            "(DATEDIFF(day, {1}, {2}) / 365.2425)");

    protected virtual SqlFunctionDefintion YearsToNow() =>
        Create("YearsToNow", [new("startDate", DataType.Datetime)], DataType.Number,
            "Returns completed years from startDate until now.",
            WithNowEnd(YearsTo()));

    protected virtual SqlFunctionDefintion MonthsTo() =>
        Create("MonthsTo",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns completed months between startDate and endDate.",
            MonthsTemplate());

    protected virtual SqlFunctionDefintion MonthsToNow() =>
        Create("MonthsToNow", [new("startDate", DataType.Datetime)], DataType.Number,
            "Returns completed months from startDate until now.",
            WithNowEnd(MonthsTo()));

    protected virtual SqlFunctionDefintion DaysTo() =>
        Create("DaysTo",
            [new("startDate", DataType.Datetime), new("endDate", DataType.Datetime)],
            DataType.Number, "Returns completed days between startDate and endDate.",
            DateSub().GetSqlTemplate(Dialect));

    protected virtual SqlFunctionDefintion DaysToNow() =>
        Create("DaysToNow", [new("startDate", DataType.Datetime)], DataType.Number,
            "Returns completed days from startDate until now.",
            WithNowEnd(DaysTo()));

    private string WithNowEnd(SqlFunctionDefintion function) =>
        function.GetSqlTemplate(Dialect).Replace("{2}", CurrentDateTimeSql, StringComparison.Ordinal);

    // Calendar-month boundaries, minus one when the end day-of-month is earlier than the start's.
    private string MonthsTemplate()
    {
        var dayOfEnd = DayPart("{2}");
        var dayOfStart = DayPart("{1}");
        return $"({MonthSub().GetSqlTemplate(Dialect)} - CASE WHEN {dayOfEnd} < {dayOfStart} THEN 1 ELSE 0 END)";
    }

    private string DayPart(string date) =>
        Day().GetSqlTemplate(Dialect).Replace("{1}", date, StringComparison.Ordinal);

    protected virtual SqlFunctionDefintion Lag() =>
        Create("Lag",
            [new("expression", DataType.Number), new("offset", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns the previous row's value in ascending order.",
            "LAG({1}, {2}) OVER (ORDER BY {3} ASC)");

    protected virtual SqlFunctionDefintion WindowSum() =>
        Create("WindowSum",
            [new("expression", DataType.Number), new("orderBy", DataType.String)],
            DataType.Number, "Returns a running total in ascending order.",
            "SUM({1}) OVER (ORDER BY {2} ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)");

    protected virtual SqlFunctionDefintion Rank() =>
        Create("Rank",
            [new("partitionBy", DataType.Number), new("orderBy", DataType.Number)],
            DataType.Number, "Ranks rows within each partition in descending order.",
            "RANK() OVER (PARTITION BY {1} ORDER BY {2} DESC)");

    protected virtual SqlFunctionDefintion RowNumber() =>
        Create("RowNumber",
            [new("partitionBy", DataType.Number), new("orderBy", DataType.Datetime)],
            DataType.Number, "Numbers rows independently within each partition in descending order.",
            "ROW_NUMBER() OVER (PARTITION BY {1} ORDER BY {2} DESC)");

    protected virtual SqlFunctionDefintion Any() =>
        CreateCollectionPredicate("Any", "EXISTS",
            "Returns true when a related collection has a row matching the optional predicate.");

    protected virtual SqlFunctionDefintion NotAny() =>
        CreateCollectionPredicate("NotAny", "NOT EXISTS",
            "Returns true when no related collection row matches the optional predicate.",
            negatesCollection: true);

    private SqlFunctionDefintion CreateCollectionPredicate(
        string name,
        string existsOperator,
        string description,
        bool negatesCollection = false)
        => new(name,
            [new("predicate", DataType.Boolean, omittable: true)],
            DataType.Boolean,
            description,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Dialect] = $"{existsOperator} (SELECT 1 FROM @Context WHERE {{1}} AND @JoinedConditions)"
            },
            functionApplyFor: FunctionApplyFor.DataSet,
            negatesCollection: negatesCollection);

    protected SqlFunctionDefintion Create(
        string name,
        IReadOnlyList<SqlFunctionArgumentDefinition> arguments,
        DataType resultType,
        string description,
        string sqlTemplate,
        bool bindArgumentsFromEnd = false,
        FunctionApplyFor? functionApplyFor = null,
        string? windowOf = null)
        => new(name, arguments, resultType, description,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Dialect] = sqlTemplate
            },
            bindArgumentsFromEnd,
            functionApplyFor ?? InferApplyFor(name, arguments),
            windowOf: windowOf);

    private static FunctionApplyFor InferApplyFor(
        string name,
        IReadOnlyList<SqlFunctionDefintion.SqlFunctionArgumentDefinition> arguments)
    {
        if (BuiltInAggregateFunctions.Contains(name))
            return FunctionApplyFor.DataSet;
        if (arguments.Count == 0)
            return FunctionApplyFor.DataSet;
        return arguments[0].DataType switch
        {
            DataType.String => FunctionApplyFor.Text,
            DataType.Number => FunctionApplyFor.Number,
            DataType.Datetime => FunctionApplyFor.DateTime,
            DataType.Boolean => FunctionApplyFor.Boolean,
            _ => FunctionApplyFor.DataSet
        };
    }

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
