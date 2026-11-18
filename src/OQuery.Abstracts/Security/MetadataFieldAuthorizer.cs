#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IMetadataFieldAuthorizer
{
    bool CanReadField(string entityName, string fieldName);
}

public sealed class MetadataFieldAuthorizer : IMetadataFieldAuthorizer
{
    private const string ALL_FIELDS = "*";
    private readonly HashSet<string> _permissions;
    private readonly AccessMode _accessMode;

    public MetadataFieldAuthorizer(
        IEnumerable<MetadataFieldPermission>? permissions = null,
        AccessMode accessMode = AccessMode.Deny)
    {
        _accessMode = accessMode;
        _permissions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var permission in permissions ?? [])
        {
            if (permission.Fields is not null)
                foreach (var field in permission.Fields)
                    _permissions.Add(CreateKey(permission.EntityName, field));
            else
                _permissions.Add(CreateKey(permission.EntityName, ALL_FIELDS));
        }
    }

    public bool CanReadField(string entityName, string fieldName)
    {
        if (_permissions.Count == 0)
            return _accessMode == AccessMode.Allow;

        var isConfigured =
            _permissions.Contains(CreateKey(entityName, ALL_FIELDS)) ||
            _permissions.Contains(CreateKey(entityName, fieldName));

        return _accessMode == AccessMode.Allow
            ? isConfigured
            : !isConfigured;
    }

    private static string CreateKey(string entityName, string fieldName)
        => $"{entityName}\0{fieldName}";

}
