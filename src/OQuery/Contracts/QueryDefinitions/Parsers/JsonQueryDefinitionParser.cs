using System.Text.Json;
using System.Text.Json.Serialization;
using OQuery.QueryDefinitions.Parsers.JsonConverters;

namespace OQuery.QueryDefinitions.Parsers;

/// <summary>Parses and configures JSON query-definition serialization.</summary>
public static class JsonQueryDefinitionParser
{
    public static QueryDefinition Parse(string json)
        => QueryDefinitionParser.Parse(json);

    public static JsonSerializerOptions CreateSerializerOptions(bool writeIndented = false)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = writeIndented
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy ??= JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        if (!options.Converters.Any(converter => converter is JsonStringEnumConverter))
        {
            options.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }
        AddConverter<FieldDefinitionJsonConverter>(options);
        AddConverter<ConditionDefinitionJsonConverter>(options);
        AddConverter<ConditionDefintionJsonConverter>(options);
        AddConverter<ConditionStatementDefinitionJsonConverter>(options);
        AddConverter<ProjectionDefinitionJsonConverter>(options);
    }

    private static void AddConverter<TConverter>(JsonSerializerOptions options)
        where TConverter : JsonConverter, new()
    {
        if (!options.Converters.Any(converter => converter is TConverter))
            options.Converters.Add(new TConverter());
    }
}
