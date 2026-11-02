using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
internal sealed class QueryRequestParserTests : TestBase
{
    [Test]
    public void TextToFunctionFieldDto_Arguments()
    {
        var text = "Round(Id, <2>)";
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseField(text);

        var expectedField = new FunctionFieldDefinition("Round", [
            new FieldDefinition("Id"),
            new ConstFieldDefinition(2)
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_GroupArgument()
    {
        var text = "Round(Id + <2>)";
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseField(text);

        var expectedField = new FunctionFieldDefinition("Round", [
            new GroupFieldDefintion([
                new FieldDefinition("Id"),
                new ConstFieldDefinition(2)
            ])
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_Nested()
    {
        var text = "Round(Sum(Id + Age), <2>)";
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseField(text);

        var expectedField = new FunctionFieldDefinition("Round", [
            new FunctionFieldDefinition("Sum", [
                new GroupFieldDefintion([
                    new FieldDefinition("Id"),
                    new FieldDefinition("Age")
                ])
            ]),
            new ConstFieldDefinition(2)
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToFunctionFieldDto_LegacyBracketSyntax()
    {
        var field = (FunctionFieldDefinition)QueryDefinitionParser.ParseProjection("Round[Sum[Id], <2>]")[0].Fields.Single();

        Assert.That(field.ToText(), Is.EqualTo("Round(Sum(Id), <2>)"));
    }

    [Test]
    public void TextToGroupFieldDto()
    {
        const string text = "Id + Age";
        var field = (GroupFieldDefintion)QueryDefinitionParser.ParseField(text);

        var expectedField = new GroupFieldDefintion([
            new FieldDefinition("Id"),
            new FieldDefinition("Age")
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    [Test]
    public void TextToGroupFieldDto_Nested()
    {
        const string text = "Id + (Age * Sum(Salary))";
        var field = (GroupFieldDefintion)QueryDefinitionParser.ParseField(text);

        var expectedField = new GroupFieldDefintion([
            new FieldDefinition("Id"),
            new GroupFieldDefintion([
                new FieldDefinition("Age"),
                new FunctionFieldDefinition(
                    MatchOperation.Multiply,
                    "Sum",
                    [new FieldDefinition("Salary")])
            ])
        ]);

        Assert.That(field.ToText(), Is.EqualTo(text));
        AssertEquivalent(expectedField, field);
    }

    private static void AssertEquivalent(IFieldDefinition expected, IFieldDefinition actual)
    {
        Assert.That(actual.Oper, Is.EqualTo(expected.Oper));
        switch (expected, actual)
        {
            case (FieldDefinition expectedField, FieldDefinition actualField):
                Assert.That(actualField.FieldName, Is.EqualTo(expectedField.FieldName));
                break;
            case (ConstFieldDefinition expectedConstant, ConstFieldDefinition actualConstant):
                Assert.That(actualConstant.Value, Is.EqualTo(expectedConstant.Value));
                break;
            case (FunctionFieldDefinition expectedFunction, FunctionFieldDefinition actualFunction):
                Assert.That(actualFunction.FunctionName, Is.EqualTo(expectedFunction.FunctionName));
                Assert.That(actualFunction.Arguments, Has.Length.EqualTo(expectedFunction.Arguments.Length));
                for (var index = 0; index < expectedFunction.Arguments.Length; index++)
                    AssertEquivalent(expectedFunction.Arguments[index], actualFunction.Arguments[index]);
                break;
            case (GroupFieldDefintion expectedGroup, GroupFieldDefintion actualGroup):
                Assert.That(actualGroup.Fields, Has.Length.EqualTo(expectedGroup.Fields.Length));
                for (var index = 0; index < expectedGroup.Fields.Length; index++)
                    AssertEquivalent(expectedGroup.Fields[index], actualGroup.Fields[index]);
                break;
            default:
                Assert.Fail($"Expected {expected.GetType().Name}, got {actual.GetType().Name}.");
                break;
        }
    }
}
