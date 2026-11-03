#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record ProjectionDefinition(IFieldDefinition[] Fields, string? Alias = null)
{
    public ProjectionDefinition(string field, string? alias = null)
        : this([new FieldDefinition(field)], alias)
    {
    }

    public const string NewLineAlias = "NEW_LINE";

    public bool IsNewLine
        => string.Equals(Alias, NewLineAlias, StringComparison.OrdinalIgnoreCase);

    public string ToText()
    {
        var expression = FieldDefinitionHelper.Format(Fields);
        return Alias is null ? expression : $"{expression} | {Alias}";
    }
}
