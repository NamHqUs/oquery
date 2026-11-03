using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;
using System.Text.Json;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
internal sealed class QueryDefinitionTests
{
    [Test]
    public void AdditiveExpressionIsAFlatFieldList()
    {
        var projection = QueryDefinitionParser.ParseProjection("A + B - <3>").Single();

        Assert.That(projection.Fields, Has.Length.EqualTo(3));
        Assert.That(projection.Fields[0], Is.EqualTo(new FieldDefinition("A")));
        Assert.That(projection.Fields[1], Is.EqualTo(new FieldDefinition("B")));
        Assert.That(projection.Fields[2], Is.EqualTo(
            new ConstFieldDefinition(MatchOperation.Subtract, 3)));
        Assert.That(projection.ToText(), Is.EqualTo("A + B - <3>"));
    }

    [Test]
    public void ParenthesizedArithmeticIsAGroupOperand()
    {
        var fields = QueryDefinitionParser.ParseProjection("A * (B - C + D) / <2>").Single().Fields;

        Assert.That(fields, Has.Length.EqualTo(3));
        Assert.That(fields[0], Is.EqualTo(new FieldDefinition("A")));

        var group = (GroupFieldDefintion)fields[1];
        Assert.That(group.Oper, Is.EqualTo(MatchOperation.Multiply));
        Assert.That(group.Fields, Is.EqualTo(new IFieldDefinition[]
        {
            new FieldDefinition("B"),
            new FieldDefinition(MatchOperation.Subtract, "C"),
            new FieldDefinition("D")
        }));

        Assert.That(fields[2], Is.EqualTo(
            new ConstFieldDefinition(MatchOperation.Divide, 2)));
    }

    [Test]
    public void FunctionArgumentsRetainExpressionGroups()
    {
        var function = (FunctionFieldDefinition)QueryDefinitionParser
            .ParseProjection("Round(A + B, <2>)")
            .Single()
            .Fields
            .Single();

        Assert.That(function.Arguments, Has.Length.EqualTo(2));
        var group = (GroupFieldDefintion)function.Arguments[0];
        Assert.That(group.Oper, Is.EqualTo(MatchOperation.Add));
        Assert.That(group.Fields, Is.EqualTo(new IFieldDefinition[]
        {
            new FieldDefinition("A"),
            new FieldDefinition("B")
        }));
        Assert.That(function.Arguments[1], Is.EqualTo(new ConstFieldDefinition(2)));
    }

    [Test]
    public void ConsecutiveParenthesizedOperandsKeepTheirOperators()
    {
        var fields = QueryDefinitionParser.ParseProjection("<5> - (A - B) * (C - D)").Single().Fields;

        Assert.That(fields, Has.Length.EqualTo(3));
        Assert.That(fields[0], Is.EqualTo(new ConstFieldDefinition(5)));
        var firstGroup = (GroupFieldDefintion)fields[1];
        var secondGroup = (GroupFieldDefintion)fields[2];
        Assert.That(firstGroup.Oper, Is.EqualTo(MatchOperation.Subtract));
        Assert.That(secondGroup.Oper, Is.EqualTo(MatchOperation.Multiply));
        Assert.That(firstGroup.Fields[1], Is.EqualTo(new FieldDefinition(MatchOperation.Subtract, "B")));
        Assert.That(secondGroup.Fields[1], Is.EqualTo(new FieldDefinition(MatchOperation.Subtract, "D")));
    }

    [Test]
    public void ProjectionJsonWritesOperatorOnEachExpressionNode()
    {
        var projection = QueryDefinitionParser.ParseProjection("A + B - <3>").Single();
        var options = JsonQueryDefinitionParser.CreateSerializerOptions();

        var json = JsonSerializer.Serialize(projection, options);

        Assert.That(json, Is.EqualTo(
            """{"fields":[{"kind":"field","field":"A"},{"kind":"field","field":"B"},{"oper":"-","kind":"value","value":3}],"alias":null}"""));
        var restored = JsonSerializer.Deserialize<ProjectionDefinition>(json, options)
            ?? throw new InvalidOperationException("The projection JSON did not produce a definition.");
        Assert.That(restored.ToText(), Is.EqualTo(projection.ToText()));
    }

    [Test]
    public void LegacyProjectionMathNodesDeserializeIntoOperatorFields()
    {
        const string json = """
            {
              "field": {
                "oper": "Add",
                "kind": "group",
                "fields": [
                  { "kind": "field", "field": "A" },
                  {
                    "oper": "Subtract",
                    "kind": "math",
                    "field": { "kind": "value", "value": 3 }
                  }
                ]
              },
              "alias": "Calculated"
            }
            """;

        var projection = JsonSerializer.Deserialize<ProjectionDefinition>(
            json,
            JsonQueryDefinitionParser.CreateSerializerOptions())
            ?? throw new InvalidOperationException("The legacy projection JSON did not produce a definition.");

        Assert.That(projection.Fields, Has.Length.EqualTo(2));
        Assert.That(projection.Fields[1], Is.EqualTo(
            new ConstFieldDefinition(MatchOperation.Subtract, 3L)));
        Assert.That(projection.ToText(), Is.EqualTo("A - <3> | Calculated"));
    }

    [Test]
    public void YamlRejectsRelationsReferencingUnknownContexts()
    {
        Assert.That(
            () => QueryDefinitionParser.ParseFromYml("""
                Entity: Orders
                Contexts:
                  - Id: known-id
                    Name: Known
                    Entity: KnownEntity
                ExtraRelations:
                  - RefContextId: missing-id
                """),
            Throws.TypeOf<FormatException>()
                .With.Message.EqualTo("Query relation references unknown context [missing-id]."));
    }

    [Test]
    public void JsonRejectsRelationsReferencingUnknownContexts()
    {
        Assert.That(
            () => QueryDefinitionParser.Parse("""
                {
                  "entity": "Orders",
                  "contexts": {
                    "Known": {
                      "entity": "KnownEntity"
                    }
                  },
                  "relations": [
                    { "refContext": "Missing" }
                  ]
                }
                """),
            Throws.TypeOf<FormatException>()
                .With.Message.EqualTo("Query relation references unknown context [Missing]."));
    }

    [Test]
    public void JsonDeserializationUsesModelDefaultsForOmittedProperties()
    {
        var definition = JsonSerializer.Deserialize<QueryDefinition>(
            """{"entity":"Orders"}""",
            JsonQueryDefinitionParser.CreateSerializerOptions());

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition!.Entity, Is.EqualTo("Orders"));
        Assert.That(definition.Projection, Is.Empty);
        Assert.That(definition.Filter, Is.Null);
        Assert.That(definition.Sorts, Is.Empty);
        Assert.That(definition.Page, Is.Null);
        Assert.That(definition.PageSize, Is.Null);
        Assert.That(definition.Relations, Is.Empty);
        Assert.That(definition.Contexts, Is.Empty);
        Assert.That(definition.Recursive, Is.False);
    }
}

[TestFixture]
internal sealed class QueryDefinitionDatabaseTests : TestBase
{
    [Test]
    public void OperatorFieldsRespectArithmeticPrecedence()
    {
        var query = new QueryDefinition
        {
            Entity = "User",
            Projection =
            [
                new ProjectionDefinition(
                    QueryDefinitionParser.ParseExpression("<1> + <2> * <3> - <3>"),
                    "Calculated")
            ]
        };

        var result = _db.Query(query).ToList();

        Assert.That(result[0]["Calculated"], Is.EqualTo(4m));
    }
}
