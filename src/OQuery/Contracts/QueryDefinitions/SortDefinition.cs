#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public enum QuerySortDirection
{
    Ascending,
    Descending
}

public sealed record SortDefinition(
    IFieldDefinition Field,
    QuerySortDirection Direction = QuerySortDirection.Ascending);