using OQuery.QueryBuilderDto.Parsers;
using System.Globalization;

namespace OQuery.Contracts.QueryDefinitions.Parsers;

internal sealed class ConditionDefinitionParser
{
    private readonly List<Token> _tokens;
    private readonly bool _parseJoinExpressions;
    private int _position;

    public static ConditionDefinition? Parse(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : new ConditionDefinitionParser(text).Parse();

    public static IConditionDefintion? ParseJoinCondition(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : new ConditionDefinitionParser(text, parseJoinExpressions: true).Parse();

    private ConditionDefinitionParser(string text, bool parseJoinExpressions = false)
    {
        _tokens = Tokenize(text);
        _parseJoinExpressions = parseJoinExpressions;
    }

    private ConditionDefinition Parse()
    {
        if (_tokens.Count == 1)
            throw new FormatException("The filter cannot be empty.");

        var expression = ParseOr();
        Expect(TokenType.End);
        return expression as ConditionDefinition
            ?? new ConditionDefinition(LogicalOperator.And, expression);
    }

    private IConditionDefintion ParseOr()
        => ParseGroup(ParseAnd, "OR", LogicalOperator.Or);

    private IConditionDefintion ParseAnd()
        => ParseGroup(ParsePrimary, "AND", LogicalOperator.And);

    private IConditionDefintion ParseGroup(
        Func<IConditionDefintion> operand,
        string separator,
        LogicalOperator logicalOperator)
    {
        var expressions = new List<IConditionDefintion> { operand() };

        while (Match(separator))
        {
            var next = operand();
            if (next is ConditionStatementDefinition filter)
                next = filter with { LogicalOperator = logicalOperator };

            expressions.Add(next);
        }

        return expressions.Count == 1
            ? expressions[0]
            : new ConditionDefinition(logicalOperator, expressions.ToArray());
    }

    private IConditionDefintion ParsePrimary()
    {
        if (Match("("))
        {
            var grouped = ParseOr();
            Expect(")");
            return grouped;
        }

        var left = Next();
        if (left.Type is not (TokenType.Identifier or TokenType.Field or TokenType.FieldExpression))
            throw new FormatException($"Expected a filter field, received [{left.Text}].");
        var leftField = new FieldDefinition(left.Text);
        var operationToken = Next();
        if (operationToken.Type != TokenType.Operator)
            throw new FormatException($"Expected a filter operator, received [{operationToken.Text}].");
        var operation = operationToken.Text.ToLowerInvariant();
        if (operation is "not" or "is")
        {
            var modifier = Next();
            operation = $"{operation} {modifier.Text.ToLowerInvariant()}";
            if (operation == "is not")
                operation = $"{operation} {Next().Text.ToLowerInvariant()}";
        }
        var value = operation is "is null" or "is not null"
            ? null
            : ParseFilterValue(operation);

        var filterOperator = operation switch
        {
            "=" or "==" => BinaryOperator.Equals,
            "<>" or "!=" => BinaryOperator.NotEquals,
            "like" => BinaryOperator.Like,
            "not like" => BinaryOperator.NotLike,
            ">" => BinaryOperator.GreaterThan,
            "<" => BinaryOperator.LessThan,
            ">=" => BinaryOperator.GreaterThanOrEqual,
            "<=" => BinaryOperator.LessThanOrEqual,
            "between" => BinaryOperator.Between,
            "not between" => BinaryOperator.NotBetween,
            "in" => BinaryOperator.In,
            "not in" => BinaryOperator.NotIn,
            "is null" => BinaryOperator.IsNull,
            "is not null" => BinaryOperator.IsNotNull,
            _ => throw new FormatException($"Unsupported filter operator [{operation}].")
        };

        return new ConditionStatementDefinition(LogicalOperator.And, leftField, filterOperator, value)
        {
            Parameterized = true
        };
    }

    private IFieldDefinition? ParseFilterValue(string operation)
    {
        var token = Next();
        if (operation is "between" or "not between" or "in" or "not in")
            return ParseMultipleFilterValue(token);

        return token.Type switch
        {
            TokenType.Value => new ConstFieldDefinition(token.Value),
            TokenType.Field => new FieldDefinition(token.Text),
            TokenType.FieldExpression => token.Text.Equals("<NULL>", StringComparison.OrdinalIgnoreCase)
                ? null
                : FieldDefinitionParser.Parse(token.Text),
            TokenType.Identifier => operation is "==" or "!="
                ? new ConstFieldDefinition(token.Text)
                : FieldDefinitionParser.Parse(token.Text),
            _ => throw new FormatException($"Expected a filter value, received [{token.Text}].")
        };
    }

    private IFieldDefinition ParseMultipleFilterValue(Token token)
    {
        if (token.Type != TokenType.FieldExpression ||
            token.Text.Length < 2 ||
            token.Text[0] != '<' ||
            token.Text[^1] != '>')
            throw new FormatException("Multiple filter values must use the <value1,value2> format.");

        var values = token.Text[1..^1]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => _parseJoinExpressions
                ? ParseJoinExpression(value)
                : (IFieldDefinition)new ConstFieldDefinition(ParseValue(value)))
            .ToArray();
        if (values.Length == 0)
            throw new FormatException("Multiple filter values cannot be empty.");

        return new GroupFieldDefintion(values);
    }

    private static IFieldDefinition ParseJoinExpression(string value)
    {
        if (value.Length >= 2 && value[0] is '\'' or '"' && value[^1] == value[0])
            return new ConstFieldDefinition(ParseValue(value));

        return ConvertNumericFields(FieldDefinitionParser.Parse(value));
    }

    private static IFieldDefinition ConvertNumericFields(IFieldDefinition expression)
        => expression switch
        {
            FieldDefinition { FieldName: var fieldName } field
                when int.TryParse(fieldName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                     decimal.TryParse(fieldName, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
                     bool.TryParse(fieldName, out _) ||
                     fieldName.Equals("null", StringComparison.OrdinalIgnoreCase)
                => new ConstFieldDefinition(field.Oper, ParseValue(fieldName)),
            GroupFieldDefintion group => group with
            {
                Fields = group.Fields.Select(ConvertNumericFields).ToArray()
            },
            FunctionFieldDefinition function => function with
            {
                Arguments = function.Arguments.Select(ConvertNumericFields).ToArray()
            },
            _ => expression
        };

    private Token Next()
    {
        if (_position >= _tokens.Count)
            throw new FormatException("Unexpected end of filter.");

        return _tokens[_position++];
    }

    private Token Expect(TokenType type)
    {
        var token = Next();
        if (token.Type != type)
            throw new FormatException($"Expected {type}, received [{token.Text}].");
        return token;
    }

    private void Expect(string text)
    {
        var token = Next();
        if (!token.Text.Equals(text, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Expected [{text}], received [{token.Text}].");
    }

    private bool Match(string text)
    {
        if (_position >= _tokens.Count ||
            !_tokens[_position].Text.Equals(text, StringComparison.OrdinalIgnoreCase))
            return false;

        _position++;
        return true;
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        for (var index = 0; index < text.Length;)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            if (text[index] == '@')
            {
                index++;
                AddValueToken(text, ref index, tokens);

                continue;
            }

            if (text[index] is '\'' or '"')
            {
                AddValueToken(text, ref index, tokens);
                continue;
            }

            if (text[index] == '[')
            {
                var start = ++index;
                while (index < text.Length && text[index] != ']')
                    index++;
                if (index >= text.Length)
                    throw new FormatException("Unterminated field identifier.");

                var field = text[start..index];
                if (field.Length == 0)
                    throw new FormatException("Field identifier cannot be empty.");

                tokens.Add(new Token(TokenType.Field, field, null));
                index++;
                continue;
            }

            if (text[index] == '<' &&
                tokens.Count > 0 &&
                tokens[^1].Type == TokenType.Operator &&
                IsComparisonOperator(tokens[^1].Text))
            {
                var closingBracket = text.IndexOf('>', index + 1);
                if (closingBracket >= 0)
                {
                    var expression = text[index..(closingBracket + 1)];
                    tokens.Add(new Token(TokenType.FieldExpression, expression, null));
                    index = closingBracket + 1;
                    continue;
                }
            }

            if (IsIdentifierStart(text[index]))
            {
                var identifierEnd = index + 1;
                while (identifierEnd < text.Length && IsIdentifierPart(text[identifierEnd]))
                    identifierEnd++;

                var functionName = text[index..identifierEnd];
                if (identifierEnd < text.Length &&
                    text[identifierEnd] == '(' &&
                    !IsFilterKeyword(functionName) &&
                    TryFindClosingParenthesis(text, identifierEnd, out var closingParenthesis))
                {
                    var expression = text[index..(closingParenthesis + 1)];
                    tokens.Add(new Token(TokenType.FieldExpression, expression, null));
                    index = closingParenthesis + 1;
                    continue;
                }
            }

            if (index + 1 < text.Length &&
                new[] { "&&", "||", "==", "!=", ">=", "<=", "<>" }.Contains(text[index..(index + 2)]))
            {
                var operation = text[index..(index + 2)] switch
                {
                    "&&" => "AND",
                    "||" => "OR",
                    var value => value
                };
                tokens.Add(new Token(TokenType.Operator, operation, null));
                index += 2;
                continue;
            }

            if ("()=><+-*/".Contains(text[index]))
            {
                var value = text[index].ToString();
                tokens.Add(new Token(
                    text[index] is '(' or ')' ? TokenType.Parenthesis : TokenType.Operator,
                    value,
                    null));
                index++;
                continue;
            }

            var startIdentifier = index;
            while (index < text.Length &&
                   (char.IsLetterOrDigit(text[index]) || text[index] is '_' or '.'))
                index++;
            if (startIdentifier == index)
                throw new FormatException($"Unexpected filter character [{text[index]}].");

            var identifier = text[startIdentifier..index];
            tokens.Add(new Token(
                IsFilterKeyword(identifier)
                    ? TokenType.Operator
                    : int.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                      decimal.TryParse(identifier, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
                      identifier.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      identifier.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                      identifier.Equals("null", StringComparison.OrdinalIgnoreCase)
                        ? TokenType.Value
                    : TokenType.Identifier,
                identifier,
                IsFilterKeyword(identifier)
                    ? null
                    : ParseValue(identifier)));
        }

        tokens.Add(new Token(TokenType.End, string.Empty, null));
        return tokens;
    }

    private static bool TryFindClosingParenthesis(string text, int openingParenthesis, out int closingParenthesis)
    {
        var depth = 0;
        var quote = '\0';
        var inField = false;

        for (var index = openingParenthesis; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    if (index + 1 < text.Length && text[index + 1] == quote)
                        index++;
                    else
                        quote = '\0';
                }
                continue;
            }

            if (inField)
            {
                if (character == ']')
                    inField = false;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '[')
            {
                inField = true;
                continue;
            }

            if (character == '(')
                depth++;
            else if (character == ')' && --depth == 0)
            {
                closingParenthesis = index;
                return true;
            }
        }

        closingParenthesis = -1;
        return false;
    }

    private static bool IsIdentifierStart(char character)
        => char.IsLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character)
        => char.IsLetterOrDigit(character) || character is '_' or '.';

    private static bool IsFilterKeyword(string identifier)
        => identifier.Equals("and", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("or", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("like", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("not", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("between", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("in", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("is", StringComparison.OrdinalIgnoreCase);

    private static void AddValueToken(
        string text,
        ref int index,
        ICollection<Token> tokens)
    {
        if (index < text.Length && text[index] is '\'' or '"')
        {
            var quote = text[index];
            index++;
            var value = new System.Text.StringBuilder();
            while (index < text.Length)
            {
                if (text[index] == quote && index + 1 < text.Length && text[index + 1] == quote)
                {
                    value.Append(quote);
                    index += 2;
                    continue;
                }
                if (text[index] == quote)
                    break;
                value.Append(text[index++]);
            }
            if (index >= text.Length)
                throw new FormatException("Unterminated quoted filter value.");

            var parsedValue = value.ToString();
            tokens.Add(new Token(TokenType.Value, parsedValue, parsedValue));
            index++;
            return;
        }

        var valueStart = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) &&
               !"()&|=<>!".Contains(text[index]))
            index++;

        var valueText = text[valueStart..index];
        tokens.Add(new Token(TokenType.Value, valueText, ParseValue(valueText)));
    }

    private static object? ParseValue(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? integer
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : bool.TryParse(value, out var boolean)
                    ? boolean
                    : value.Equals("null", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : value;

    private static bool IsComparisonOperator(string operation)
        => operation is "=" or "==" or "!=" or "<>" or ">" or "<" or ">=" or "<=" ||
            operation.Equals("LIKE", StringComparison.OrdinalIgnoreCase) ||
            operation.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase) ||
            operation.Equals("NOT BETWEEN", StringComparison.OrdinalIgnoreCase) ||
            operation.Equals("IN", StringComparison.OrdinalIgnoreCase) ||
            operation.Equals("NOT IN", StringComparison.OrdinalIgnoreCase);

    private enum TokenType
    {
        Identifier,
        Field,
        FieldExpression,
        Operator,
        Parenthesis,
        Value,
        End
    }

    private sealed record Token(TokenType Type, string Text, object? Value);
}
