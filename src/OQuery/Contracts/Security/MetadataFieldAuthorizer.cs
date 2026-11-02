#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

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
        string yml,
        AccessMode accessMode = AccessMode.Deny)
        : this(ParseFromYml(yml), accessMode)
    {
    }

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

    public static MetadataFieldAuthorizer FromYml(string yml)
        => new(ParseFromYml(yml));

    public static MetadataFieldAuthorizer FromYmlFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return new(ParseFromYml(File.ReadAllText(filePath)));
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

    private static MetadataFieldPermission[] ParseFromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);

        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yml));

            if (yaml.Documents.Count == 0 ||
                yaml.Documents[0].RootNode is not YamlSequenceNode sequence)
                throw new FormatException("The YAML permissions must be a sequence.");

            return sequence.Children
                .Select(ParsePermission)
                .ToArray();
        }
        catch (YamlException exception)
        {
            throw new FormatException(
                $"YAML permission error: {exception.Message}",
                exception);
        }
    }

    private static MetadataFieldPermission ParsePermission(YamlNode node)
    {
        if (node is not YamlMappingNode mapping)
            throw new FormatException("A YAML permission must be an object.");

        string? entityName = null;
        string[]? fields = null;

        foreach (var entry in mapping.Children)
        {
            var propertyName = (entry.Key as YamlScalarNode)?.Value;
            switch (propertyName)
            {
                case "entityName":
                    entityName = (entry.Value as YamlScalarNode)?.Value;
                    break;
                case "fields":
                    fields = ParseFields(entry.Value);
                    break;
            }
        }

        return new MetadataFieldPermission(
            entityName
                ?? throw new FormatException("A YAML permission requires an entityName."),
            fields);
    }

    private static string[]? ParseFields(YamlNode node)
    {
        if (node is YamlScalarNode { Value: null })
            return null;

        if (node is not YamlSequenceNode sequence)
            throw new FormatException("A YAML permission fields value must be a sequence.");

        return sequence.Children
            .Select(field => (field as YamlScalarNode)?.Value
                ?? throw new FormatException("A YAML permission field must be a string."))
            .ToArray();
    }

}
