using OQuery.QueryDefinitions.Parsers;

namespace OQuery.UnitTest.Setup;

internal static class QueryDefinitionLoader
{
    public static QueryDefinition Load(string path)
        => QueryDefinitionJsonParser.Parse(File.ReadAllText(path));
}
