#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

// Aggregates are built-in SQL grouping functions, so the engine identifies them by name.
public static class BuiltInAggregateFunctions
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "Count", "Sum", "Avg", "Min", "Max",
        "CountIf", "SumIf", "AvgIf", "MinIf", "MaxIf",
        "MedicalLossRatio",
    };

    public static bool Contains(string functionName) => Names.Contains(functionName);
}
