#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public record MetadataFieldPermission(
        string EntityName,
        string[]? Fields);
