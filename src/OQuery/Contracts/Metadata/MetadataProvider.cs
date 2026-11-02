using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable IDE0130
namespace OQuery;
#pragma warning restore IDE0130

public interface IMetadataProvider
{
    Entity GetEntity(string entityName);
}

public class MetadataProvider : IMetadataProvider
{
    private readonly Dictionary<string, Entity> _entities;

    public MetadataProvider(IEnumerable<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        _entities = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<Entity> Entities => _entities.Values;

    public Entity GetEntity(string entityName)
    {
        if (_entities.TryGetValue(entityName, out Entity? entity))
            return entity;

        throw new EntityNotFoundException(entityName);
    }

    public static MetadataProvider FromYml(string yml)
    {
        ArgumentNullException.ThrowIfNull(yml);
        return FromYml(yml, "<string>");
    }

    public static MetadataProvider FromYmlFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return FromYml(File.ReadAllText(filePath), filePath);
    }

    private static MetadataProvider FromYml(string yml, string sourceName)
    {
        try
        {
            var entities = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<List<Entity>>(yml)
                ?? throw new FormatException("The YAML metadata cannot be null.");

            return new MetadataProvider(entities);
        }
        catch (YamlException exception)
        {
            throw CreateYamlFormatException(exception, sourceName, yml);
        }
    }

    private static FormatException CreateYamlFormatException(
        YamlException exception,
        string sourceName,
        string yml)
    {
        var lineNumber = exception.Start.Line;
        var columnNumber = exception.Start.Column;
        var lines = yml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var row = lineNumber > 0 && lineNumber <= lines.Length
            ? lines[lineNumber - 1]
            : string.Empty;
        var markerColumn = (int)Math.Min(int.MaxValue, Math.Max(0, columnNumber - 1));
        var marker = new string(' ', markerColumn) + "^";
        var reason = exception.InnerException?.Message ?? exception.Message;

        return new FormatException(
            $"YAML metadata error in '{sourceName}' at line {lineNumber}, column {columnNumber}: {reason}{Environment.NewLine}" +
            $"Row {lineNumber}: {row}{Environment.NewLine}" +
            $"           {marker}",
            exception);
    }
}
