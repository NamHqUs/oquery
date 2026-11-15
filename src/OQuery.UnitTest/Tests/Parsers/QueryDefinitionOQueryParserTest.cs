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
    public void ParseProjectionItemsSeparatedByIndentedLines()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            User: Id, Name
                Manager.Name
                Sum(Id)
            """);

        Assert.That(query.Projections!.Select(projection => projection.ToText()).ToArray(), Is.EqualTo([
            "Id",
            "Name",
            "Manager.Name",
            "Sum(Id)"
        ]));
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
    public void ParseProjectionAllowsClauseKeywordAsTheOnlyAlias()
    {
        var query = QueryDefinitionOQueryParser.Parse("User: Count(Id) Order");
        var projection = query.Projections!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(projection.Field.ToText(), Is.EqualTo("Count(Id)"));
            Assert.That(projection.Alias, Is.EqualTo("Order"));
        });
    }

    [Test]
    public void ParseProjectionRejectsClauseKeywordAfterExistingAlias()
    {
        Assert.That(
            () => QueryDefinitionOQueryParser.Parse("User: Count(Id) | TotalMembers Order"),
            Throws.TypeOf<FormatException>()
                .With.Message.EqualTo(
                    "Projection [Count(Id) TotalMembers Order] has unexpected trailing text [Order]. " +
                    "Clauses such as Order must start on their own line."));
    }

    [Test]
    public void ParseProjectionDoesNotTreatAngleBracketValueAsClauseKeyword()
    {
        var query = QueryDefinitionOQueryParser.Parse("User: <Order>");

        Assert.That(query.Projections!.Single().Field.ToText(), Is.EqualTo("<Order>"));
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
            Ref2 is User:
                Join Ref1 on ManagerId = Ref1.Id
                Select Ref1.Name ManagerName
            User:
                Join Ref1 on ManagerId = Ref1.Id
                Join Ref2 on ManagerId = Ref2.Id
                Select Id, Name, Ref1.Name Manager1, Ref2.ManagerName Manager2
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
    public void ParseJoinBeforeProjectionEndsJoinAtLineEnd()
    {
        var oquery = """
            DiagnosisBurden is Visits:
              Left Join VisitLabs On Id = VisitLabs.VisitId
                Select Diagnose.Name Diagnosis,
                CountDistinct(Id) VisitCount
              Where Status = <Completed>
              Order VisitCount DESC
              Take 10
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery);

        Assert.Multiple(() =>
        {
            Assert.That(query.Entity, Is.EqualTo("Visits"));
            Assert.That(query.Joins, Has.Count.EqualTo(1));
            Assert.That(query.Joins![0].RefContext, Is.EqualTo("VisitLabs"));
            Assert.That(query.Joins[0].Condition!.ToText(), Is.EqualTo("Id = VisitLabs.VisitId"));
            Assert.That(
                query.Projections!.Select(projection => projection.ToText()),
                Is.EqualTo(["Diagnose.Name Diagnosis", "CountDistinct(Id) VisitCount"]));
            Assert.That(query.Filter, Is.Not.Null);
            Assert.That(query.Sorts, Has.Count.EqualTo(1));
            Assert.That(query.Take, Is.EqualTo(10));
        });
    }

    [Test]
    public void ParseDiagnosisBurdenStatementWithJoinBeforeProjection()
    {
        var oquery = """
            VisitLabs is LabResults:
                VisitId,
                Count(Id) LabCount,
                CountIf(IsAbnormal = <true>) AbnormalCount

            DiagnosisBurden is Visits:
              Left Join VisitLabs On Id = VisitLabs.VisitId
                Select Diagnosis.Name Diagnosis,
                Diagnosis.Category DiagnosisCategory,
                Location.City ClinicCity,
                CountDistinct(Id) VisitCount,
                CountDistinct(UserId) UniquePatients,
                AVG(Charge).ROUND(<2>) AverageCharge,
                (100.0 * Sum(IfNull(VisitLabs.AbnormalCount, <0>))
                    / NullIf(Sum(IfNull(VisitLabs.LabCount, <0>)), <0>)).Round(<2>) AbnormalLabPercent
              Where Status = <Completed>
              Order VisitCount DESC, AbnormalLabPercent DESC
              Take 10
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery);
        var visitLabs = query.Contexts!["VisitLabs"];

        Assert.Multiple(() =>
        {
            Assert.That(query.Entity, Is.EqualTo("Visits"));
            Assert.That(query.Contexts.Keys, Is.EquivalentTo(["VisitLabs"]));
            Assert.That(query.Joins, Has.Count.EqualTo(1));
            Assert.That(query.Joins![0].RefContext, Is.EqualTo("VisitLabs"));
            Assert.That(query.Joins[0].JoinType, Is.EqualTo(QueryJoinType.Left));
            Assert.That(query.Joins[0].Condition!.ToText(), Is.EqualTo("Id = VisitLabs.VisitId"));
            Assert.That(
                query.Projections!.Select(projection => projection.Alias),
                Is.EqualTo([
                    "Diagnosis",
                    "DiagnosisCategory",
                    "ClinicCity",
                    "VisitCount",
                    "UniquePatients",
                    "AverageCharge",
                    "AbnormalLabPercent"
                ]));
            Assert.That(query.Filter, Is.Not.Null);
            Assert.That(query.Sorts, Has.Count.EqualTo(2));
            Assert.That(query.Take, Is.EqualTo(10));

            Assert.That(visitLabs.Entity, Is.EqualTo("LabResults"));
            Assert.That(
                visitLabs.Projections!.Select(projection => projection.Alias),
                Is.EqualTo([null, "LabCount", "AbnormalCount"]));
        });
    }

    [Test]
    public void ParseJoinContinuesUntilTopLevelComma()
    {
        var oquery = """
            Visits is Visits:
              Left Join VisitLabs On Id = VisitLabs.VisitId AND
                VisitLabs.IsActive = <true>,
                Select Id
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery);

        Assert.That(query.Joins![0].Condition!.ToText(),
            Is.EqualTo("Id = VisitLabs.VisitId AND VisitLabs.IsActive = <True>"));
        Assert.That(
            query.Projections!.Select(projection => projection.ToText()),
            Is.EqualTo(["Id"]));
    }

    [Test]
    public void ParseRejectsMultiLineJoinWithoutCommaBeforeProjection()
    {
        var oquery = """
            Visits: Id
              Left Join VisitLabs
                On Id = VisitLabs.VisitId
                Name
            """;

        Assert.That(
            () => QueryDefinitionOQueryParser.Parse(oquery),
            Throws.TypeOf<FormatException>()
                .With.Message.Contains("A multi-line join must end with a comma"));
    }

    [Test]
    public void ParseMultiLineJoinEndedByCommaBeforeProjection()
    {
        var oquery = """
            Visits:
              Left Join VisitLabs
                On Id = VisitLabs.VisitId,
                Select Name
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery);

        Assert.Multiple(() =>
        {
            Assert.That(query.Joins![0].Condition!.ToText(), Is.EqualTo("Id = VisitLabs.VisitId"));
            Assert.That(query.Projections!.Select(projection => projection.ToText()), Is.EqualTo(["Name"]));
        });
    }

    [Test]
    public void ParseRejectsStrayLineAfterJoinWhenProjectionAlreadyDefined()
    {
        var oquery = """
            Visits: Id
              Left Join VisitLabs
                On Id = VisitLabs.VisitId
                Name
            """;

        Assert.That(
            () => QueryDefinitionOQueryParser.Parse(oquery),
            Throws.TypeOf<FormatException>()
                .With.Message.Contains("Unexpected line [Name] after a join"));
    }

    [Test]
    public void ParseRejectsDefinitionsWithoutEntryPoint()
    {
        var oquery = """
            ContextA is User:
              Join ContextB on Id = ContextB.Id
              Select Id
            ContextB is User:
              Join ContextA on Id = ContextA.Id
              Select Id
            """;

        Assert.That(
            () => QueryDefinitionOQueryParser.Parse(oquery),
            Throws.TypeOf<FormatException>()
                .With.Message.EqualTo("need entiry point"));
    }

    [Test]
    public void ParseSelectedContextIgnoresUnrelatedInvalidBlocks()
    {
        var oquery = """
            Products:

            ref1 is Products:

            ref2 is Products: Count(Id)asfsdfs
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery, "Products");

        Assert.That(query.Entity, Is.EqualTo("Products"));
        Assert.That(query.Contexts, Is.Empty);
        Assert.That(query.Projections, Is.Empty);
    }

    [Test]
    public void ParseSelectedContextIgnoresUnrelatedInvalidHeaders()
    {
        var oquery = """
            Products:
            D
            Departments is Products: Id
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery, "Departments");

        Assert.That(query.Entity, Is.EqualTo("Products"));
        Assert.That(query.Projections!.Select(projection => projection.ToText()), Is.EqualTo(["Id"]));
    }

    [Test]
    public void ParseSelectedContextIncludesNamedDependencies()
    {
        var oquery = """
            Products:
            ref1 is Products: Id
            ref2 is ref1: *, Count(Id) Total
            """;

        var query = QueryDefinitionOQueryParser.Parse(oquery, "ref2");

        Assert.That(query.Entity, Is.EqualTo("ref1"));
        Assert.That(query.Contexts!.Keys, Is.EqualTo(["ref1"]));
        Assert.That(query.Contexts["ref1"].Entity, Is.EqualTo("Products"));
    }

    [Test]
    public void ParseRecursiveProjection()
    {
        var oquery = """
            Loop is User:
              INNER JOIN Loop member on Id = member.ManagerId
              Select Name ManagerName, member.FirstName MemberName
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
    public void ParseSelectedRecursiveContext()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            EmployeeTree is Employees:
              INNER JOIN EmployeeTree member on Id = member.ManagerId
              Select Name ManagerName,
                member.Name MemberName
            """, "EmployeeTree");

        Assert.That(query.Contexts, Does.ContainKey("EmployeeTree"));
        Assert.That(query.Contexts!["EmployeeTree"].Projections!
            .Select(projection => projection.ToText())
            .ToArray(), Is.EqualTo(["Name ManagerName", "EmployeeTree.ManagerName MemberName"]));
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

    [TestCase("Take 10")]
    [TestCase("Take <10>")]
    public void ParseTakeAcceptsBareAndAngleBracketNumericValues(string clause)
    {
        var query = QueryDefinitionOQueryParser.Parse($"""
            User: Id
              {clause}
            """);

        Assert.That(query.Take, Is.EqualTo(10));
    }

    [Test]
    public void ParseUnindentedWhereClause()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            ProductSales is OrderItems: ProductId,
                SUM(Quantity * UnitPrice) TotalRevenue,
                TotalRevenue.Avg() Average
            WHERE TotalRevenue > Average
            """);

        Assert.That(query.Filter!.ToText(), Is.EqualTo("TotalRevenue > Average"));
    }

    [Test]
    public void ParseRejectsMultipleWhereClausesInOneQuery()
    {
        var oquery = """
            User: Id
              Where A = <1>
              Where B = <2>
            """;

        Assert.That(
            () => QueryDefinitionOQueryParser.Parse(oquery),
            Throws.TypeOf<FormatException>()
                .With.Message.Contains("Multiple Where clauses are not allowed"));
    }

    [Test]
    public void ParseCombinesConditionsWithExplicitAndOrInOneWhere()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            User: Id
              Where A = <1> AND B = <2> OR C = <3>
            """);

        Assert.That(query.Filter, Is.Not.Null);
    }

    [TestCase("User: Id\n  Join VisitLabs on Id = VisitLabs.VisitId", "explicit Select")]
    [TestCase("User:\n  Join VisitLabs on Id = VisitLabs.VisitId\n  Join Other on Id = Other.Id\n  Id", "Start the projection with Select")]
    public void ParseRequiresExplicitSelectWhenQueryHasJoin(string text, string expectedMessage)
    {
        Assert.That(
            () => QueryDefinitionOQueryParser.Parse(text),
            Throws.TypeOf<FormatException>().With.Message.Contains(expectedMessage));
    }

    [Test]
    public void ParseAcceptsSelectAfterMultipleJoins()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            User:
              Join VisitLabs on Id = VisitLabs.VisitId
              Join Other on Id = Other.Id
              Select Id, Name
            """);

        Assert.That(query.Joins, Has.Count.EqualTo(2));
        Assert.That(query.Projections!.Select(projection => projection.ToText()), Is.EqualTo(["Id", "Name"]));
    }

}
