using OQuery.Contracts.QueryDefinitions.Parsers;

namespace OQuery.Test;

[TestFixture]
internal class _23_FilterParserTest : TestBase
{
    [Test]
    public void FilterParserKeepsFlatConditionsFlatAndPreservesExplicitGroups()
    {
        var flatFilter = QueryDefinitionParser.ParseFilter(
            "Name = <Alice> AND Email = <alice@example.com> AND Id = <1>");
        var explicitlyGroupedFilter = QueryDefinitionParser.ParseFilter(
            "Name = <Alice> AND (Email = <alice@example.com> AND Id = <1>)");

        Assert.That(flatFilter, Is.Not.Null);
        Assert.That(flatFilter!.Filters, Has.Count.EqualTo(3));
        Assert.That(flatFilter.Filters, Has.None.InstanceOf<ConditionDefinition>());
        Assert.That(flatFilter.ToText(),
            Is.EqualTo("Name = <Alice> AND Email = <alice@example.com> AND Id = <1>"));

        const string requestText = """
            Entity: User
            Projection:
              - Name
            Filter: Name = <Alice Johnson> AND Email = <alice.johnson@example.com> AND Id = <1>
            """;
        var parsedRequest = QueryDefinitionParser.ParseFromYml(requestText);

        Assert.That(parsedRequest.Filter!.Filters, Has.Count.EqualTo(3));
        Assert.That(parsedRequest.Filter.Filters, Has.None.InstanceOf<ConditionDefinition>());

        Assert.That(explicitlyGroupedFilter, Is.Not.Null);
        Assert.That(explicitlyGroupedFilter!.Filters, Has.Count.EqualTo(2));
        Assert.That(explicitlyGroupedFilter.Filters[1], Is.InstanceOf<ConditionDefinition>());
        Assert.That(explicitlyGroupedFilter.ToText(),
            Is.EqualTo("Name = <Alice> AND (Email = <alice@example.com> AND Id = <1>)"));
    }

    [Test]
    public void Filter_FromText()
    {
        var query = new QueryDefinition
        {
            Entity = "Invoices",
            Projection = QueryDefinitionParser.ParseProjection("Year(InvoiceDate) | Year, Month(InvoiceDate) | Month, COUNT() | InvoiceCount, SUM(TotalAmount) | Revenue, AVG(TotalAmount) | AverageOrderValue"),
            Filter = QueryDefinitionParser.ParseFilter("Status <> <Draft> And Sum(TotalAmount) > 200"),
        };

        var queryText = query.ToString();
        Assert.That(queryText, Does.Contain("Filter: Status <> <Draft> AND Sum(TotalAmount) > <200>"));

        var data = _db.Query(query).ToList();
        Assert.That(data.Count, Is.GreaterThan(0));
    }
}
