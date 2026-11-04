#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public sealed record ProjectionDefinition(IFieldDefinition Field, string? Alias = null)
{
    public ProjectionDefinition(string field, string? alias = null)
        : this(new FieldDefinition(field), alias)
    {
    }

    public string ToText()
    {
        return Alias is null ? Field.ToText() : $"{Field.ToText()} | {Alias}";
    }
}
