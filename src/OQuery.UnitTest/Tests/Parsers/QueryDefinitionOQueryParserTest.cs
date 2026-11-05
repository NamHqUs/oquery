using OQuery.QueryDefinitions.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace OQuery.UnitTest.Tests.Parsers;

[TestFixture]
internal class QueryDefinitionOQueryParserTest : TestBase
{
    [TestCase("User:", "User")]
    [TestCase("SomeAlias is User: Id", "User")]
    public void ParseEntity(string input, string expectedEntity)
    {
        var query = QueryDefinitionOQueryParser.Parse(input);
        Assert.That(query.Entity, Is.EqualTo(expectedEntity));
    }

    [TestCase("User")]
    [TestCase("SomeAlias is User")]
    public void ParseEntityWithoutProjectionRejectsInput(string input)
    {
        Assert.That(
            () => QueryDefinitionOQueryParser.Parse(input),
            Throws.TypeOf<FormatException>());
    }

    [TestCase("User: Id, Name", new string[]{ "Id", "Name" })]
    [TestCase("User: Id,\n Name", new string[]{ "Id", "Name" })]
    [TestCase("User: Id,\r\n Name", new string[]{ "Id", "Name" })]
    public void ParseProjection(string input, string[] expectedProjection)
    {
        var query = QueryDefinitionOQueryParser.Parse(input);
        Assert.That(query.Entity, Is.EqualTo("User"));
        Assert.That(query.Projections!.Select(p => (p.Field as FieldDefinition)!.FieldName).ToArray(), Is.EqualTo(expectedProjection));
    }

    [Test]
    public void ParseProjectionAliasSeparatedByWhitespace()
    {
        var query = QueryDefinitionOQueryParser.Parse("User: Ref1.Name ManagerName");
        var projection = query.Projections!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(projection.Field.ToText(), Is.EqualTo("Ref1.Name"));
            Assert.That(projection.Alias, Is.EqualTo("ManagerName"));
            Assert.That(projection.ToText(), Is.EqualTo("Ref1.Name ManagerName"));
        });
    }

    [Test]
    public void ParseProjectionAliasWithAsKeyword()
    {
        var query = QueryDefinitionOQueryParser.Parse("User: Name as myname");
        var projection = query.Projections!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(projection.Field.ToText(), Is.EqualTo("Name"));
            Assert.That(projection.Alias, Is.EqualTo("myname"));
        });
    }

    [Test]
    public void ParseConcatenatedProjectionWithAsAlias()
    {
        var query = QueryDefinitionOQueryParser.Parse(
            "Orders: Id, OrderDate, User.FirstName + < > + User.LastName as Fullname");

        Assert.That(query.Projections!.Last().Alias, Is.EqualTo("Fullname"));
    }

    [Test]
    public void ParseContexts()
    {
        var oqurey = """
            Ref1 is User: Id, Name
            Ref2 is User: Ref1.Name ManagerName
                Join Ref1 on ManagerId = Ref1.Id
            User: Id, Name, Ref1.Name Manager1, Ref2.ManagerName Manager2
                Join Ref1 on ManagerId = Ref1.Id
                Join Ref2 on ManagerId = Ref2.Id
            """;

        var query = QueryDefinitionOQueryParser.Parse(oqurey);
        var ref1 = query.Contexts!["Ref1"];
        var ref2 = query.Contexts["Ref2"];

        Assert.Multiple(() =>
        {
            Assert.That(query.Entity, Is.EqualTo("User"));
            Assert.That(query.Contexts.Keys, Is.EquivalentTo(["Ref1", "Ref2"]));
            Assert.That(
                query.Projections!.Select(projection => projection.ToText()),
                Is.EqualTo(["Id", "Name", "Ref1.Name Manager1", "Ref2.ManagerName Manager2"]));
            Assert.That(query.Joins, Has.Count.EqualTo(2));
            Assert.That(
                query.Joins!.Select(join => join.RefContext),
                Is.EqualTo(["Ref1", "Ref2"]));
            Assert.That(
                query.Joins!.Select(join => join.JoinType),
                Is.EqualTo([QueryJoinType.Inner, QueryJoinType.Inner]));
            Assert.That(
                query.Joins!.Select(join => join.Condition!.ToText()),
                Is.EqualTo(["ManagerId = Ref1.Id", "ManagerId = Ref2.Id"]));
            Assert.That(ref1.Entity, Is.EqualTo("User"));
            Assert.That(
                ref1.Projections!.Select(projection => projection.ToText()),
                Is.EqualTo(["Id", "Name"]));
            Assert.That(ref1.Joins, Is.Empty);
            Assert.That(ref2.Entity, Is.EqualTo("User"));
            Assert.That(
                ref2.Projections!.Select(projection => projection.ToText()),
                Is.EqualTo(["Ref1.Name ManagerName"]));
            Assert.That(ref2.Joins, Has.Count.EqualTo(1));
            Assert.That(ref2.Joins![0].RefContext, Is.EqualTo("Ref1"));
            Assert.That(ref2.Joins[0].JoinType, Is.EqualTo(QueryJoinType.Inner));
            Assert.That(ref2.Joins[0].Condition!.ToText(), Is.EqualTo("ManagerId = Ref1.Id"));
        });
    }

    [Test]
    public void ParseRecursiveProjection()
    {
        var oquery = """
            Loop is User: Name ManagerName, member.FirstName MemberName
              INNER JOIN Loop member on Id = member.ManagerId
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery);
        var recursive = query.Contexts!["Loop"];

        Assert.Multiple(() =>
        {
            Assert.That(query.Entity, Is.EqualTo("User"));
            Assert.That(query.Contexts.Keys, Is.EqualTo(["Loop"]));
            Assert.That(
                query.Projections!.Select(projection => projection.ToText()),
                Is.EqualTo(["Name ManagerName", "member.FirstName MemberName"]));
            Assert.That(query.Joins, Has.Count.EqualTo(1));
            Assert.That(query.Joins![0].RefContext, Is.EqualTo("Loop"));
            Assert.That(query.Joins[0].Alias, Is.EqualTo("member"));
            Assert.That(query.Joins[0].JoinType, Is.EqualTo(QueryJoinType.Inner));
            Assert.That(query.Joins[0].Condition!.ToText(), Is.EqualTo("Id = member.ManagerId"));
            Assert.That(recursive.Entity, Is.EqualTo("User"));
            Assert.That(
                recursive.Projections!.Select(projection => projection.ToText()),
                Is.EqualTo(["Name ManagerName", "Loop.FirstName MemberName"]));
            Assert.That(recursive.Joins, Is.Null);
        });
    }

    [Test]
    public void ParseSkip()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            User: Id
              Skip 10
            """);

        Assert.That(query.Skip, Is.EqualTo(10));
    }

}
