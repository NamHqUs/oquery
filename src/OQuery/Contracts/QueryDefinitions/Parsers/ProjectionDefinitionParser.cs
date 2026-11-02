using OQuery;
using OQuery.QueryBuilderDto.Parsers;

namespace OQuery.QueryDefinitions.Parsers;

internal class ProjectionDefinitionParser
{
    public static List<ProjectionDefinition> Parse(List<string?>? texts)
        => texts?.Where(e => !string.IsNullOrWhiteSpace(e)).SelectMany(Parse!).ToList() ?? [];

    public static List<ProjectionDefinition> Parse(string text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : FieldDefinitionParser.SplitTopLevel(text, ',')
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(FromText)
            .ToList();

    static ProjectionDefinition FromText(string text)
    {
        var separatorIndex = text.IndexOf('|');
        var fieldText = separatorIndex < 0 ? text.Trim() : text[..separatorIndex].Trim();
        var alias = separatorIndex < 0 ? null : text[(separatorIndex + 1)..].Trim();
        return new ProjectionDefinition(FieldDefinitionParser.ParseExpression(fieldText), alias);
    }
}
