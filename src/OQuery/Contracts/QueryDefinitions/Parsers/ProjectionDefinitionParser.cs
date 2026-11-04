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
        var separatorIndex = text.IndexOf('|');
        var fieldText = separatorIndex < 0 ? text.Trim() : text[..separatorIndex].Trim();
        var alias = separatorIndex < 0 ? null : text[(separatorIndex + 1)..].Trim();
        if (fieldText.Length == 0)
            return null;
        var fields = FieldDefinitionParser.ParseExpression(fieldText);
        var field = fields.Length == 1
            ? fields[0]
            : new GroupFieldDefintion(fields);
        return new ProjectionDefinition(field, alias);
    }
}
