using OQuery.QueryBuilderDto.Parsers;

namespace OQuery.Contracts.QueryDefinitions.Parsers;

internal static class SortDefinitionParser
{
    public static List<SortDefinition> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return FieldDefinitionParser.SplitTopLevel(text, ',')
            .Where(sort => !string.IsNullOrWhiteSpace(sort))
            .Select(ParseSort)
            .ToList();
    }

    private static SortDefinition ParseSort(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("A sort expression cannot be empty.");

        var directionSeparator = FindTopLevelLastWhitespace(text);
        var fieldText = directionSeparator < 0 ? text.Trim() : text[..directionSeparator].Trim();
        var directionText = directionSeparator < 0 ? null : text[(directionSeparator + 1)..].Trim();

        if (directionText is not null && TryParseDirection(directionText, out var direction))
        {
            if (fieldText.Length == 0)
                throw new FormatException("A sort requires a field expression.");
            return new SortDefinition(FieldDefinitionParser.Parse(fieldText), direction);
        }

        if (directionText is not null &&
            directionText.All(char.IsLetter) &&
            !EndsWithFieldOperator(fieldText))
            throw new FormatException($"Sort direction [{directionText}] is invalid.");

        return new SortDefinition(FieldDefinitionParser.Parse(text.Trim()));
    }

    private static bool TryParseDirection(string text, out QuerySortDirection direction)
    {
        if (text.Equals("ASC", StringComparison.OrdinalIgnoreCase) ||
            text.Equals(nameof(QuerySortDirection.Ascending), StringComparison.OrdinalIgnoreCase))
        {
            direction = QuerySortDirection.Ascending;
            return true;
        }

        if (text.Equals("DESC", StringComparison.OrdinalIgnoreCase) ||
            text.Equals(nameof(QuerySortDirection.Descending), StringComparison.OrdinalIgnoreCase))
        {
            direction = QuerySortDirection.Descending;
            return true;
        }

        direction = default;
        return false;
    }

    private static int FindTopLevelLastWhitespace(string text)
    {
        var lastWhitespace = -1;
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var index = 0; index < text.Length; index++)
        {
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
                case '<':
                    constraints++;
                    break;
                case '>' when constraints > 0:
                    constraints--;
                    break;
            }

            if (char.IsWhiteSpace(text[index]) &&
                parentheses == 0 && brackets == 0 && constraints == 0)
                lastWhitespace = index;
        }

        return lastWhitespace;
    }

    private static bool EndsWithFieldOperator(string text)
        => text.Length > 0 && text[^1] is '+' or '-' or '*' or '/' or '%' or '=' or '>' or '<';
}
