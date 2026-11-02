#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public record MetadataPermission(
    string EntityName,
    string[]? Fields);

public interface IMetadataPermission
{
    bool CanReadField(string entityName, string fieldName);
}

public sealed class PermissionManagement : IMetadataPermission
{
    private const string ALL_FIELDS = "*";
    private readonly HashSet<string> _permissions;
    private readonly bool _isDenied;

    public PermissionManagement(
        IEnumerable<MetadataPermission>? permissions = null,
        bool IsDenied = true)
    {
        _isDenied = IsDenied;
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
            return !_isDenied;

        return 
            _permissions.Contains(CreateKey(entityName, ALL_FIELDS)) ||
            _permissions.Contains(CreateKey(entityName, fieldName))
            ? !_isDenied
            : _isDenied;
    }

    private static string CreateKey(string entityName, string fieldName)
        => $"{entityName}\0{fieldName}";
}
