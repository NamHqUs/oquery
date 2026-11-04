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
    public void OQueryParsesMultilineProjectionBeforeClauses()
    {
        var definition = OQueryDefinitionParser.Parse("""
            Visits: Diagnosis.Name | Diagnosis,
                Diagnosis.Category | DiagnosisCategory,
                ROUND(<100> * SUM(Amount)
                  / NULLIF(SUM(Total), <0>), <2>) | Percent
              where Status = <Completed>
              take 10
            """);

        Assert.That(definition.Projections.Select(field => field.ToText()),
            Is.EqualTo([
                "Diagnosis.Name | Diagnosis",
                "Diagnosis.Category | DiagnosisCategory",
                "ROUND(<100> * SUM(Amount) / NULLIF(SUM(Total), <0>), <2>) | Percent"
            ]));
        Assert.That(definition.Take, Is.EqualTo(10));
    }

    [Test]
    public void OQueryParsesMultilineOrderAndContextProjection()
    {
        var definition = OQueryDefinitionParser.Parse("""
            Visits: Diagnosis.Name | Diagnosis,
                Diagnosis.Category | DiagnosisCategory
              order VisitCount ASC,
                Diagnosis.Name ASC
              left join VisitLabs on Id = VisitLabs.VisitId
            VisitLabs is LabResults: VisitId | VisitId,
                COUNT(Id) | LabCount
            """);

        Assert.That(definition.Sorts.Select(sort => sort.Field.ToText()),
            Is.EqualTo(["VisitCount", "Diagnosis.Name"]));
        Assert.That(definition.Joins!.Single().RefContext, Is.EqualTo("VisitLabs"));
        Assert.That(definition.Contexts!["VisitLabs"].Projections.Select(field => field.ToText()),
            Is.EqualTo(["VisitId | VisitId", "COUNT(Id) | LabCount"]));
    }

    [Test]
    public void OQueryInfersSelectedConsumingContextAsRoot()
    {
        var definition = OQueryDefinitionParser.Parse("""
            UserContext1 is User: Id, Name
            UserContext2 is User: Id, Name, ManagerId
              Left Join UserContext1 on ManagerId = UserContext1.Id
            """);

        Assert.That(definition.Entity, Is.EqualTo("User"));
        Assert.That(definition.Contexts, Does.ContainKey("UserContext1"));
        Assert.That(definition.Joins!.Single().RefContext, Is.EqualTo("UserContext1"));
        Assert.That(definition.Projections.Select(field => field.ToText()),
            Is.EqualTo(["Id", "Name", "ManagerId"]));
    }

    [Test]
    public void OQueryKeepsRootWhenSelectionIncludesAllRelatedContexts()
    {
        var definition = OQueryDefinitionParser.Parse("""
            User: Id, Name
              Left Join UserContext1 on ManagerId = UserContext1.Id
              Left Join UserContext2 on ManagerId = UserContext2.Id
            UserContext1 is User: Id, Name
            UserContext2 is User: Id, Name, ManagerId
              Left Join UserContext1 on ManagerId = UserContext1.Id
            """);

        Assert.That(definition.Entity, Is.EqualTo("User"));
        Assert.That(definition.Joins!.Select(relation => relation.RefContext),
            Is.EqualTo(["UserContext1", "UserContext2"]));
        Assert.That(definition.Contexts!["UserContext2"].Joins!.Single().RefContext,
            Is.EqualTo("UserContext1"));
    }

    [Test]
    public void OQueryAllowsOneContextToBeJoinedMultipleTimesWithAliases()
    {
        var definition = OQueryDefinitionParser.Parse("""
            User: Id, Name, ManagerId, ref1.Name | Manager1, newRef1.Name | Manager2
              Left Join ref1 On ManagerId = ref1.Id
              Left Join ref1 newRef1 On ManagerId = newRef1.Id
            ref1 is User: Id, Name
            """);

        Assert.That(definition.Joins, Has.Count.EqualTo(2));
        Assert.That(definition.Joins![0].RefContext, Is.EqualTo("ref1"));
        Assert.That(definition.Joins[0].Alias, Is.Null);
        Assert.That(definition.Joins[1].RefContext, Is.EqualTo("ref1"));
        Assert.That(definition.Joins[1].Alias, Is.EqualTo("newRef1"));
        Assert.That(() => QueryDefinitionParser.ValidateContextReferences(definition),
            Throws.Nothing);
    }

    [Test]
    public void JsonPreservesNullJoinTypesDespiteIgnoringOtherNullProperties()
    {
        var definition = new QueryDefinition("User")
        {

            Joins = [new("Managers", JoinType: null)],
            Contexts = new()
            {
                ["Managers"] = new QueryDefinitionBase("User")
                {

                    Joins = [new("Managers", JoinType: null)]
                }
            }
        };
        var options = JsonQueryDefinitionParser.CreateSerializerOptions();
        var json = JsonSerializer.Serialize(definition, options);
        Assert.That(json, Does.Contain("\"joinType\":null"));
        var deserialized = JsonSerializer.Deserialize<QueryDefinition>(json, options)
            ?? throw new InvalidOperationException("The query did not deserialize.");
        Assert.That(deserialized.Joins!.Single().JoinType, Is.Null);
        Assert.That(deserialized.Contexts!["Managers"].Joins!.Single().JoinType, Is.Null);
        var parsed = QueryDefinitionParser.Parse("""
            {
              "entity": "User",
              "relations": [{ "refContext": "Managers", "joinType": null }],
              "contexts": { "Managers": { "entity": "User" } }
            }
            """);
        Assert.That(parsed.Joins!.Single().JoinType, Is.Null);
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
        Assert.That(definition.Projections, Is.Null);
        Assert.That(definition.Filter, Is.Null);
        Assert.That(definition.Sorts, Is.Null);
        Assert.That(definition.Skip, Is.Null);
        Assert.That(definition.Take, Is.Null);
        Assert.That(definition.Joins, Is.Null);
        Assert.That(definition.Contexts, Is.Empty);
    }

}

[TestFixture]
internal sealed class QueryDefinitionDatabaseTests : TestBase
{

    [Test]
    public void OperatorFieldsRespectArithmeticPrecedence()
    {
        var query = new QueryDefinition("User")
        {

            Projections =
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
