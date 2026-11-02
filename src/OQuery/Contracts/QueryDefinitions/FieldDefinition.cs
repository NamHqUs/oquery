#pragma warning disable IDE0130
using OQuery.QueryDefinitions.Parsers;
using System.Text.Json.Serialization;

namespace OQuery;

[JsonConverter(typeof(FieldDefinitionJsonConverter))]
public interface IFieldDefinition
{
    MatchOperation Oper { get; }
    string ToText();
}

public record FieldDefinition(MatchOperation Oper , string FieldName) : IFieldDefinition
{
    public FieldDefinition(string fieldName) : this(MatchOperation.Add, fieldName) { }
    public string ToText() => FieldName;
}

public record ConstFieldDefinition(MatchOperation Oper, object? Value) : IFieldDefinition
{
    public ConstFieldDefinition(object? value) : this(MatchOperation.Add, value) { }
    public string ToText() => $"<{Value}>";
}
public record FunctionFieldDefinition(MatchOperation Oper, string FunctionName, IFieldDefinition[] Arguments) : IFieldDefinition
{
    public FunctionFieldDefinition(string functionName, IFieldDefinition[] arguments) : this(MatchOperation.Add, functionName, arguments) { }
    public string ToText() => $"{FunctionName}({string.Join(", ", Arguments.Select(argument =>
        argument is GroupFieldDefintion group
            ? FieldDefinitionHelper.Format(group.Fields)
            : argument.ToText()))})";
}

public record GroupFieldDefintion(MatchOperation Oper, IFieldDefinition[] Fields) : IFieldDefinition
{
    public GroupFieldDefintion(IFieldDefinition[] fields)  : this(MatchOperation.Add, fields) {  }

    public string ToText()
        => FieldDefinitionHelper.Format(Fields);
}

public record ConditionFieldDefinition(MatchOperation Oper, IConditionDefintion Condition) : IFieldDefinition
{
    public ConditionFieldDefinition(IConditionDefintion condition)
        : this(MatchOperation.Add, condition)
    {
    }

    public string ToText() => Condition.ToText();
}

public enum MatchOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulus,
    BitwiseAnd,
    BitwiseOr,
}

internal static class FieldDefinitionHelper
{
    public static string Format(IEnumerable<IFieldDefinition> fields)
        => string.Join(" ", fields.Select((field, index) =>
            $"{(index == 0 ? string.Empty : field.Oper.ToSymbol() + " ")}{FormatOperand(field)}"));

    private static string FormatOperand(IFieldDefinition field)
        => field is GroupFieldDefintion ? $"({field.ToText()})" : field.ToText();
}

internal static class MatchOperationExtensions
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
