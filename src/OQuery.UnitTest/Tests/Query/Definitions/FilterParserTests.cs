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

            Projections = QueryDefinitionParser.ParseProjection("Year(InvoiceDate) | Year, Month(InvoiceDate) | Month, COUNT(Id) | InvoiceCount, SUM(TotalAmount) | Revenue, AVG(TotalAmount) | AverageOrderValue"),
            Filter = QueryDefinitionParser.ParseFilter("Status <> <Draft> And Sum(TotalAmount) > 200"),
        };

        var queryText = OQueryDefinitionParser.Write(query);
        Assert.That(queryText, Does.Contain("Where Status <> <Draft> AND Sum(TotalAmount) > <200>"));

        var data = _db.Query(query).ToList();
        Assert.That(data.Count, Is.GreaterThan(0));
    }
}
