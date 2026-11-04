using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(typeInfo =>
            {
                if (typeInfo.Type == typeof(QueryDefinition) ||
                    typeInfo.Type == typeof(QueryDefinitionBase))
                {
                    var projectionName = options.PropertyNamingPolicy?.ConvertName(nameof(QueryDefinitionBase.Projections))
                        ?? nameof(QueryDefinitionBase.Projections);
                    foreach (var property in typeInfo.Properties)
                        if (property.Name == projectionName)
                            property.Name = "projection";
                }

                if (typeInfo.Type != typeof(JoinDefinition))
                    return;
                var joinTypeName = options.PropertyNamingPolicy?.ConvertName(nameof(JoinDefinition.JoinType))
                    ?? nameof(JoinDefinition.JoinType);
                foreach (var property in typeInfo.Properties)
                    if (property.Name == joinTypeName)
                        property.ShouldSerialize = (_, _) => true;
            });
    }

    private static void AddConverter<TConverter>(JsonSerializerOptions options)
        where TConverter : JsonConverter, new()
    {
        if (!options.Converters.Any(converter => converter is TConverter))
            options.Converters.Add(new TConverter());
    }
}
