namespace OQuery;

internal static class MatchOperationParser
{
    public static string ToSymbol(this MatchOperation operation)
        => operation switch
        {
            MatchOperation.Add => "+",
            MatchOperation.Subtract => "-",
            MatchOperation.Multiply => "*",
            MatchOperation.Divide => "/",
            MatchOperation.Modulus => "%",
            MatchOperation.BitwiseAnd => "&",
            MatchOperation.BitwiseOr => "|",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported match operation.")
        };

    public static MatchOperation Parse(string operation)
        => operation switch
        {
            "+" => MatchOperation.Add,
            "-" => MatchOperation.Subtract,
            "*" => MatchOperation.Multiply,
            "/" => MatchOperation.Divide,
            "%" => MatchOperation.Modulus,
            "&" => MatchOperation.BitwiseAnd,
            "|" => MatchOperation.BitwiseOr,
            _ => throw new FormatException($"Unsupported match operator [{operation}].")
        };

    public static MatchOperation ParseArithmetic(string operation)
    {
        var parsed = Parse(operation);
        return parsed.IsArithmetic()
            ? parsed
            : throw new FormatException($"Unsupported arithmetic operator [{operation}].");
    }

    public static bool IsArithmetic(this MatchOperation operation)
        => operation is MatchOperation.Add or MatchOperation.Subtract or MatchOperation.Multiply
            or MatchOperation.Divide or MatchOperation.Modulus;
}
