namespace OQuery.QueryDefinitions.Parsers;

public static class OQueryDefinitionFileParser
{
    public static QueryDefinition Parse(FileInfo file)
        => OQueryDefinitionParser.Parse(file);

    public static string Write(QueryDefinition definition)
        => OQueryDefinitionParser.Write(definition);
}
