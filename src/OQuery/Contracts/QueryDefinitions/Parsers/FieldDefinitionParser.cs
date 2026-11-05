#pragma warning disable IDE0130
using OQuery;

namespace OQuery.QueryBuilderDto.Parsers;

internal static class FieldDefinitionParser
{
    public static IFieldDefinition Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        text = text.Trim();

        if (TryGetFunctionCall(text, out var opening))
        {
            var argumentsText = text[(opening + 1)..^1];
            var arguments = SplitTopLevel(argumentsText, ',')
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument => LooksLikePredicateExpression(argument)
                    ? new FieldDefinition(argument)
                    : ParseArgument(argument))
                .ToArray();
            return new FunctionFieldDefinition(text[..opening].Trim(), arguments);
        }

        if (IsWrappedBy(text, '(', ')'))
        {
            return new GroupFieldDefintion(ParseExpression(text[1..^1]));
        }

        if (IsWrappedBy(text, '<', '>'))
        {
            var value = text[1..^1];
            return new ConstFieldDefinition(ParseConstant(value));
        }

        return ParseSequence(text);
    }

    public static IFieldDefinition[] ParseExpression(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        text = text.Trim();

        if (IsWrappedBy(text, '(', ')'))
            return ParseExpression(text[1..^1]);
        if (TryGetFunctionCall(text, out _) || IsWrappedBy(text, '<', '>'))
            return [Parse(text)];

        return ParseSequenceFields(text);
    }

    private static IFieldDefinition ParseArgument(string text)
    {
        if (IsWrappedBy(text, '(', ')'))
            return new GroupFieldDefintion(ParseExpression(text[1..^1]));

        var fields = ParseExpression(text);
        return fields.Length == 1 ? fields[0] : new GroupFieldDefintion(fields);
    }

    private static bool TryGetFunctionCall(string text, out int opening)
    {
        var openingParenthesis = text.IndexOf('(');
        var openingBracket = text.IndexOf('[');

        if (openingParenthesis > 0 &&
            text[..openingParenthesis].All(character => !char.IsWhiteSpace(character)) &&
            IsWrappedBy(text[openingParenthesis..], '(', ')'))
        {
            opening = openingParenthesis;
            return true;
        }

        if (openingBracket > 0 && IsWrappedBy(text[openingBracket..], '[', ']'))
        {
            opening = openingBracket;
            return true;
        }

        opening = -1;
        return false;
    }

    private static bool LooksLikePredicateExpression(string text)
        => ContainsTopLevel(text, "=") ||
            ContainsTopLevel(text, "<>") ||
            ContainsTopLevel(text, " < ") ||
            ContainsTopLevel(text, " > ") ||
            ContainsKeyword(text, "IS") ||
            ContainsKeyword(text, "LIKE") ||
            ContainsKeyword(text, "IN");

    private static bool ContainsTopLevel(string text, string token)
    {
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var index = 0; index <= text.Length - token.Length; index++)
        {
            if (parentheses == 0 &&
                brackets == 0 &&
                constraints == 0 &&
                text.AsSpan(index, token.Length).SequenceEqual(token))
            {
                return true;
            }

            switch (text[index])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<' when parentheses == 0 && brackets == 0:
                    constraints++;
                    break;
                case '>' when parentheses == 0 && brackets == 0 && constraints > 0:
                    constraints--;
                    break;
            }
        }

        return false;
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        var start = 0;
        while ((start = text.IndexOf(keyword, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (!IsTopLevelAt(text, start))
            {
                start += keyword.Length;
                continue;
            }

            var beforeIsBoundary = start == 0 || !char.IsLetterOrDigit(text[start - 1]);
            var afterIndex = start + keyword.Length;
            var afterIsBoundary = afterIndex == text.Length || !char.IsLetterOrDigit(text[afterIndex]);
            if (beforeIsBoundary && afterIsBoundary)
                return true;
            start = afterIndex;
        }

        return false;
    }

    private static bool IsTopLevelAt(string text, int index)
    {
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var position = 0; position < index; position++)
        {
            switch (text[position])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<' when parentheses == 0 && brackets == 0:
                    constraints++;
                    break;
                case '>' when parentheses == 0 && brackets == 0 && constraints > 0:
                    constraints--;
                    break;
            }
        }

        return parentheses == 0 && brackets == 0 && constraints == 0;
    }

    private static object? ParseConstant(string value)
    {
        if (value.Length == 0 || value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;
        if (int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var integer))
            return integer;
        if (decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowExponent,
            System.Globalization.CultureInfo.InvariantCulture,
            out var number))
            return number;
        return bool.TryParse(value, out var boolean) ? boolean : value;
    }

    private static IFieldDefinition ParseSequence(string text)
    {
        var fields = ParseSequenceFields(text);
        return fields.Length == 1 ? fields[0] : new GroupFieldDefintion(fields);
    }

    private static IFieldDefinition[] ParseSequenceFields(string text)
    {
        var parts = SplitBinaryOperators(text);
        if (parts.Count == 1)
            return [ParseAtom(parts[0])];

        var fields = new List<IFieldDefinition> { ParseAtom(parts[0]) };
        for (var index = 1; index < parts.Count; index += 2)
        {
            var operation = MatchOperationParser.ParseArithmetic(parts[index]);
            var operand = ParseAtom(parts[index + 1]);
            fields.Add(WithOperation(operand, operation));
        }

        return fields.ToArray();
    }

    private static IFieldDefinition WithOperation(IFieldDefinition field, MatchOperation operation)
        => field switch
        {
            FieldDefinition value => value with { Oper = operation },
            ConstFieldDefinition value => value with { Oper = operation },
            FunctionFieldDefinition value => value with { Oper = operation },
            GroupFieldDefintion value => value with { Oper = operation },
            _ => throw new FormatException("A mathematical operator requires a supported field expression.")
        };

    private static IFieldDefinition ParseAtom(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            throw new FormatException("A field expression cannot contain an empty operand.");
        }

        if (IsWrappedBy(text, '(', ')') ||
            IsWrappedBy(text, '<', '>') ||
            TryGetFunctionCall(text, out _))
        {
            return Parse(text);
        }

        return new FieldDefinition(text);
    }

    private static List<string> SplitBinaryOperators(string text)
    {
        var parts = new List<string>();
        var start = 0;
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (parentheses == 0 && brackets == 0 && constraints == 0 &&
                IsBinaryOperator(text, i, out var operatorLength))
            {
                parts.Add(text[start..i].Trim());
                parts.Add(text.Substring(i, operatorLength));
                i += operatorLength - 1;
                start = i + 1;
                continue;
            }

            switch (text[i])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<' when parentheses == 0 && brackets == 0:
                    constraints++;
                    break;
                case '>' when parentheses == 0 && brackets == 0 && constraints > 0:
                    constraints--;
                    break;
            }
        }

        parts.Add(text[start..].Trim());
        if (parts.Count > 1 && (parts.Count % 2 == 0 || parts.Any(string.IsNullOrWhiteSpace)))
        {
            throw new FormatException("The field expression contains an invalid binary operator.");
        }
        return parts;
    }

    private static bool IsBinaryOperator(string text, int index, out int operatorLength)
    {
        operatorLength = 1;
        if (index == 0)
        {
            return false;
        }

        if (text[index] is '+' or '-' or '*' or '/' or '%')
        {
            operatorLength = 1;
        }
        else if (text[index] is '=' or '>' or '<')
        {
            if (text[index] == '<' &&
                index + 1 < text.Length &&
                char.IsWhiteSpace(text[index + 1]))
            {
                return false;
            }
            if (index + 1 < text.Length && text[index + 1] == '=')
            {
                operatorLength = 2;
            }
            else if (text[index] == '<' && index + 1 < text.Length && text[index + 1] == '>')
            {
                operatorLength = 2;
            }
        }
        else
        {
            return false;
        }

        return index + operatorLength < text.Length &&
            char.IsWhiteSpace(text[index - 1]) &&
            char.IsWhiteSpace(text[index + operatorLength]);
    }

    internal static List<string> SplitTopLevel(string text, char delimiter)
    {
        var parts = new List<string>();
        var start = 0;
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<':
                    constraints++;
                    break;
                case '>':
                    if (constraints > 0)
                    {
                        constraints--;
                    }
                    break;
            }

            if (text[i] == delimiter && parentheses == 0 && brackets == 0 && constraints == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(text[start..].Trim());
        return parts;
    }

    private static bool IsWrappedBy(string text, char opening, char closing)
    {
        if (text.Length < 2 || text[0] != opening || text[^1] != closing)
        {
            return false;
        }

        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == opening)
            {
                depth++;
            }
            else if (text[i] == closing && --depth == 0)
            {
                return i == text.Length - 1;
            }
        }
        return false;
    }
}
