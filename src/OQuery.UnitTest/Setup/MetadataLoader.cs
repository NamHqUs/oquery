namespace OQuery.UnitTest.Setup;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class MetadataLoader
{
    public static IReadOnlyCollection<Entity> SaleDemoData { get; } = Load("sale-demo.yml");
    public static IReadOnlyCollection<Entity> NorthStarDemoData { get; } = Load("northstar-demo.yml");
    public static IReadOnlyCollection<Entity> UnitTestData { get; } = Load("unit-test.yml");

    private static IReadOnlyCollection<Entity> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Metadata", fileName);
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<List<Entity>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Metadata file '{path}' is empty.");
    }
}
