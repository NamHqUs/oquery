using OQuery;
using OQuery.QueryBuilderDto.Parsers;

namespace OQuery.QueryDefinitions.Parsers;

internal class ProjectionDefinitionParser
{
    public static List<ProjectionDefinition> Parse(List<string?>? texts)
        => texts?.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => FromText(e!))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList() ?? [];

    public static List<ProjectionDefinition> Parse(string text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : FieldDefinitionParser.SplitTopLevel(text, ',')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(FromText)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

    static ProjectionDefinition? FromText(string text)
    {
        text = text.Trim();
        var fieldText = text;
        string? alias = null;
        var pipeIndex = text.IndexOf('|');
        if (pipeIndex >= 0)
        {
            fieldText = text[..pipeIndex].Trim();
            alias = text[(pipeIndex + 1)..].Trim();
        }
        else
        {
            var aliasStart = FindAliasStart(text);
            if (aliasStart >= 0)
            {
                fieldText = text[..aliasStart].TrimEnd();
                alias = text[(aliasStart + 1)..].Trim();
            }
        }
        if (fieldText.Length == 0)
            return null;
        var fields = FieldDefinitionParser.ParseExpression(fieldText);
        var field = fields.Length == 1
            ? fields[0]
            : new GroupFieldDefintion(fields);
        return new ProjectionDefinition(field, alias);
    }

    private static int FindAliasStart(string text)
    {
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var angleDepth = 0;

        for (var index = text.Length - 1; index >= 0; index--)
        {
            switch (text[index])
            {
                case ')':
                    parenthesisDepth++;
                    break;
                case '(':
                    parenthesisDepth--;
                    break;
                case ']':
                    bracketDepth++;
                    break;
                case '[':
                    bracketDepth--;
                    break;
                case '>':
                    angleDepth++;
                    break;
                case '<':
                    angleDepth--;
                    break;
                case ' ' when parenthesisDepth == 0 && bracketDepth == 0 && angleDepth == 0:
                    var alias = text[(index + 1)..].Trim();
                    if (alias.Length > 0 &&
                        alias.All(IsIdentifierPart) &&
                        IsIdentifierStart(alias[0]))
                        return index;
                    break;
            }
        }

        return -1;
    }

    private static bool IsIdentifierStart(char value)
        => char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value)
        => char.IsLetterOrDigit(value) || value == '_';
}
