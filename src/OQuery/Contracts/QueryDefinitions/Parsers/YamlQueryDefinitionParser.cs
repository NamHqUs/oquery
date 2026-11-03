namespace OQuery.QueryDefinitions.Parsers;

/// <summary>Parses and writes YAML query definitions.</summary>
public static class YamlQueryDefinitionParser
{
    public static QueryDefinition Parse(string yaml)
        => QueryDefinitionParser.ParseFromYml(yaml);

    public static QueryDefinition Parse(FileInfo file)
        => QueryDefinitionParser.ParseFromYml(file);

    public static string Write(QueryDefinition definition)
        => QueryDefinitionParser.ToYml(definition);
}
