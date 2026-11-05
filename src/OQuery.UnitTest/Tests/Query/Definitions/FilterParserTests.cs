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
}
