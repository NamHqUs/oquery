#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        string yml,
        bool IsDenied = true)
        : this(ParseFromYml(yml), IsDenied)
    {
    }

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

    public static PermissionManagement FromYml(string yml)
        => new(ParseFromYml(yml));

    public static PermissionManagement FromYmlFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return new(ParseFromYml(File.ReadAllText(filePath)));
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

    private static MetadataPermission[] ParseFromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);

        try
        {
            var permissions = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<YamlMetadataPermission[]>(yml)
                ?? throw new FormatException("The YAML permissions cannot be null.");

            return permissions
                .Select(permission => new MetadataPermission(
                    permission.EntityName
                        ?? throw new FormatException("A YAML permission requires an entityName."),
                    permission.Fields))
                .ToArray();
        }
        catch (YamlException exception)
        {
            throw new FormatException(
                $"YAML permission error: {exception.Message}",
                exception);
        }
    }

    private sealed class YamlMetadataPermission
    {
        public string? EntityName { get; set; }
        public string[]? Fields { get; set; }
    }
}
