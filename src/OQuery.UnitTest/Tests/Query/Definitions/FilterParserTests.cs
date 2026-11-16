using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
internal sealed class FilterParserTests : TestBase
{

    [Test]
    public void Filter_FromText()
    {
        var query = new QueryDefinition("Invoices")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Year(InvoiceDate) | Year, Month(InvoiceDate) | Month, COUNT(Id) | InvoiceCount, SUM(TotalAmount) | Revenue, AVG(TotalAmount) | AverageOrderValue"),
            Filter = QueryDefinitionJsonParser.ParseFilter("Status <> <Draft> And Sum(TotalAmount) > 200"),
        };

        var queryText = QueryDefinitionOQueryParser.Write(query);
        Assert.That(queryText, Does.Contain("Where Status <> <Draft> AND Sum(TotalAmount) > <200>"));

        var data = _db.Query(query).ToList();
        Assert.That(data.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Filter_AllowsParenthesizedExpressionAsValue()
    {
        var filter = QueryDefinitionJsonParser.ParseFilter("Total = (Minimum + <1>)");

        Assert.That(filter!.ToText(), Is.EqualTo("Total = Minimum + <1>"));
    }

    [Test]
    public void Filter_ParsesExistsAndNotExistsContextConditions()
    {
        var filter = QueryDefinitionJsonParser.ParseFilter(
            "(Any(Members, Name = <Bob>)) AND NotAny(Members, Name = <David>)");

        Assert.That(
            filter!.ToText(),
            Is.EqualTo("Any(Members, Name = <Bob>) AND NotAny(Members, Name = <David>)"));
    }

    [Test]
    public void Filter_FromOQueryText_SupportsIsNotNull()
    {
        var query = QueryDefinitionOQueryParser.Parse("""
            User: Name
              Where ManagerId Is Not Null
            """);

        var filter = query.Filter!.Filters.Single();
        var statement = (ConditionStatementDefinition)filter;
        var sql = _db.Query(query).ToSql();

        Assert.Multiple(() =>
        {
            Assert.That(statement.Operator, Is.EqualTo(BinaryOperator.IsNotNull));
            Assert.That(statement.Right, Is.Null);
            Assert.That(sql.CommandText, Does.Contain("IS NOT NULL"));
        });
    }

    [Test]
    public void Filter_ParsesDottedCollectionAnyAndNotAny()
    {
        var any = QueryDefinitionJsonParser.ParseFilter("Members.Any(Salary > <80000>)");
        var notAny = QueryDefinitionJsonParser.ParseFilter("Members.NotAny()");

        Assert.Multiple(() =>
        {
            Assert.That(any!.Filters.Single(), Is.TypeOf<FunctionCondition>());
            Assert.That(((FunctionCondition)any.Filters.Single()).Function.FunctionName, Is.EqualTo("Any"));
            Assert.That(((FunctionCondition)notAny!.Filters.Single()).Function.FunctionName, Is.EqualTo("NotAny"));
        });
    }
}
