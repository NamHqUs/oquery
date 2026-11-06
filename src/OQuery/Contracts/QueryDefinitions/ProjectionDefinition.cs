#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record ProjectionDefinition(IFieldDefinition Field, string? Alias = null)
{
    public string? AliasSeparator { get; init; }

    public ProjectionDefinition(string field, string? alias = null)
        : this(
            new FieldDefinition(alias is null && field.IndexOf('|') >= 0
                ? field[..field.IndexOf('|')].Trim()
                : field),
            alias ?? (field.IndexOf('|') >= 0
                ? field[(field.IndexOf('|') + 1)..].Trim()
                : null))
    {
    }

    public string ToText()
    {
        return Alias is null
            ? Field.ToText()
            : $"{Field.ToText()}{(AliasSeparator ?? " ")}{Alias}";
    }
}
