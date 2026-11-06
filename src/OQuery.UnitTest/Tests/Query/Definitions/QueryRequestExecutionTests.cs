using OQuery.QueryDefinitions.Parsers;
using OQuery.UnitTest;
using OQuery.UnitTest.Setup;
using OQuery.UnitTest.Tests.Providers;

namespace OQuery.UnitTest.Tests.Query.Definitions;

[TestFixture]
public sealed class QueryRequestExecutionTests : TestBase
{
    [Test]
    public void DefinitionBuildsParameterizedSql()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, Manager.Name | ManagerName"),
            Filter = QueryDefinitionJsonParser.ParseFilter("Age > 18"),
            Sorts =
            [
                new SortDefinition(new FieldDefinition("Name"))
            ],
            Take = 10
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("SELECT"));
        Assert.That(sql.CommandText, Does.Contain("ORDER BY"));
        Assert.That(sql.CommandText, Does.Contain(_sqlDialect.RenderPaging(0, 10, hasOrderBy: true)));
        Assert.That(sql.CommandText, Does.Contain("@p0"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 18 }));
    }

    [Test]
    public void SavedCategoryAndChannelProfitabilityGroupsProjectionAliasLayer()
    {
        var query = QueryDefinitionLoader.Load(
            SavedQueryTestCases.GetRequestPath(
                "Sales 1. Category and channel profitability.json"));

        var sql = _db.Query(query).ToSql().CommandText;
        Assert.That(
            System.Text.RegularExpressions.Regex.Matches(sql, @"(?i)\bGROUP\s+BY\b").Count,
            Is.GreaterThanOrEqualTo(2));
    }

    [TestCase(null)]
    [TestCase(0)]
    public void DefinitionDoesNotPageWhenPageSizeIsUnsetOrZero(int? pageSize)
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name"),
            Take = pageSize
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Not.Contain("OFFSET"));
        Assert.That(sql, Does.Not.Contain("FETCH NEXT"));
    }

    [Test]
    public void AggregateFilterUsesHavingInsteadOfScalarSubquery()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, AVG(Age) | AverageAge"),
            Filter = QueryDefinitionJsonParser.ParseFilter("AVG(Age) > 12"),
            Take = 50
        };

        var sql = _db.Query(definition).ToSql().CommandText;
        var nameColumn = System.Text.RegularExpressions.Regex.Escape(
            "t0." + _sqlDialect.QuoteIdentifier("Name"));
        var ageColumn = System.Text.RegularExpressions.Regex.Escape(
            "t0." + _sqlDialect.QuoteIdentifier("Age"));

        Assert.That(sql, Does.Match(@"GROUP BY\s+" + nameColumn));
        Assert.That(sql, Does.Match(@"HAVING\s+\(\s*AVG\s*\(\s*" + ageColumn + @"\s*\)\s*>"));
        Assert.That(sql, Does.Not.Match(@"(?i)\bWHERE\s*\(\s*\("));
        Assert.That(sql, Does.Not.Match(@"(?i)\bSELECT\s+AVG\b"));
    }

    [Test]
    public void ContextQualifiedAggregateFilterUsesScalarSubquery()
    {
        var definition = new QueryDefinition("Products")
        {
            Projections = QueryDefinitionJsonParser.ParseProjection(
                "Id, Name, ProductSales.TotalRevenue | TotalRevenue"),
            Joins =
            [
                new JoinDefinition(
                    "ProductSales",
                    Condition: QueryDefinitionJsonParser.ParseFilter(
                        "Id = ProductSales.ProductId")!.Filters[0],
                    JoinType: QueryJoinType.Inner),
            ],
            Filter = QueryDefinitionJsonParser.ParseFilter(
                "ProductSales.TotalRevenue > AVG(ProductSales.TotalRevenue)"),
            Contexts = new Dictionary<string, QueryDefinitionBase>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProductSales"] = new QueryDefinitionBase("OrderItems")
                {
                    Projections = QueryDefinitionJsonParser.ParseProjection(
                        "ProductId, SUM(Quantity * UnitPrice) | TotalRevenue")
                }
            }
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Match(
            @"(?is)WHERE\s+\(\s*t\d+\.\""TotalRevenue\""\s*>\s*\(\s*SELECT\s+AVG\s*\(\s*t\d+\.\""TotalRevenue\""\s*\)\s+FROM\s+\""ProductSales\"""));
        Assert.That(sql, Does.Not.Contain("HAVING"));
    }

    [Test]
    public void ProjectionAliasDependenciesUseMinimumNumberOfLayers()
    {
        var definition = new QueryDefinition("OrderItems")
        {
            Projections = QueryDefinitionJsonParser.ParseProjection(
                "ProductId, SUM(Quantity * UnitPrice) | Alias1, " +
                "AverageOver(Alias1) | Alias2, " +
                "CountOver(Alias1) | Alias3, " +
                "SumOver(Alias3) | Alias4")
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(
            System.Text.RegularExpressions.Regex.Matches(sql, @"(?i)FROM\s+\("),
            Has.Count.EqualTo(2));
        Assert.That(sql, Does.Match(
            @"(?is)AVG\s*\(\s*q0\.\""Alias1\""\s*\)\s+OVER\s*\(\s*\)\s+\""Alias2\"""));
        Assert.That(sql, Does.Match(
            @"(?is)COUNT\s*\(\s*q0\.\""Alias1\""\s*\)\s+OVER\s*\(\s*\)\s+\""Alias3\"""));
        Assert.That(sql, Does.Match(
            @"(?is)SUM\s*\(\s*q1\.\""Alias3\""\s*\)\s+OVER\s*\(\s*\)\s+\""Alias4\"""));
    }

    [Test]
    public void ProjectionAliasFilterIsAppliedAfterAliasLayers()
    {
        var definition = new QueryDefinition("OrderItems")
        {
            Projections = QueryDefinitionJsonParser.ParseProjection(
                "ProductId, SUM(Quantity * UnitPrice) | Alias1, " +
                "AverageOver(Alias1) | Alias2"),
            Filter = QueryDefinitionJsonParser.ParseFilter("Alias1 > Alias2")
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(
            System.Text.RegularExpressions.Regex.Matches(sql, @"(?i)FROM\s+\("),
            Has.Count.EqualTo(2));
        Assert.That(sql, Does.Match(
            @"(?is)SELECT\s+q1\.\""ProductId\""[\s\S]*FROM\s+\([\s\S]*\)\s+AS\s+q1"));
        Assert.That(sql, Does.Match(
            @"(?is)WHERE\s+\(\s*q1\.\""Alias1\""\s*>\s*q1\.\""Alias2\""\s*\)"));
    }

    [Test]
    public void OQueryTextSupportsUnindentedAliasFilterAfterWindowProjection()
    {
        var query = _db.QueryOqr("""
            ProductSales is OrderItems: ProductId, Product.Name,
                SUM(Quantity * UnitPrice) TotalRevenue,
                AverageOver(TotalRevenue) Average
            WHERE TotalRevenue > Average
            """);

        var sql = query.ToSql().CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Match(
                @"(?is)AVG\s*\(\s*q0\.\""TotalRevenue\""\s*\)\s+OVER\s*\(\s*\)\s+\""Average\"""));
            Assert.That(sql, Does.Match(
                @"(?is)SELECT\s+q1\.\""ProductId\""[\s\S]*FROM\s+\([\s\S]*\)\s+AS\s+q1"));
            Assert.That(sql, Does.Match(
                @"(?is)WHERE\s+\(\s*q1\.\""TotalRevenue\""\s*>\s*q1\.\""Average\""\s*\)"));
        });
    }

    [Test]
    public void OQueryTextSupportsWindowProjectionBeforeItsAggregateAlias()
    {
        var query = _db.QueryOqr("""
            OrderItems: ProductId, Product.Name,
                AverageOver(TotalRevenue) Average,
                Sum(Quantity * UnitPrice) TotalRevenue
            WHERE TotalRevenue > Average
            """);

        var sql = query.ToSql().CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Match(
                @"(?is)AVG\s*\(\s*q0\.\""TotalRevenue\""\s*\)\s+OVER\s*\(\s*\)\s+\""Average\"""));
            Assert.That(sql, Does.Match(
                @"(?is)SELECT\s+q1\.\""ProductId\""[\s\S]*FROM\s+\([\s\S]*\)\s+AS\s+q1"));
            Assert.That(sql, Does.Match(
                @"(?is)WHERE\s+\(\s*q1\.\""TotalRevenue\""\s*>\s*q1\.\""Average\""\s*\)"));
        });
    }



    [Test]
    public void QueryOqrSelectsAnExplicitDependentRootContext()
    {
        var query = _db.QueryOqr("""
            ProductSales is OrderItems:
                ProductId,
                SUM(Quantity * UnitPrice) Revenue
            ProductSalesWithAverage is ProductSales:
                ProductId,
                Revenue,
                AverageOver(Revenue) AverageRevenue
            Main is ProductSalesWithAverage:
                ProductId,
                Revenue,
                AverageRevenue
              WHERE Revenue > AverageRevenue
            """);

        var sql = query.ToSql().CommandText;

        Assert.That(sql, Does.Match(
            @"(?is)WHERE\s+\(\s*q0\.\""Revenue\""\s*>\s*q0\.\""AverageRevenue\""\s*\)"));
    }

    [Test]
    public void QueryOqrExpandsWildcardProjectionForDependentContext()
    {
        var query = _db.QueryOqr("""
            ProductSales is OrderItems:
                ProductId,
                SUM(Quantity * UnitPrice) Revenue
            ProductSalesWithAverage is ProductSales:
                *,
                AverageOver(Revenue) AverageRevenue
            ProductSalesWithAverage:
                *
              WHERE Revenue > AverageRevenue
            """, "ProductSalesWithAverage");

        var sql = query.ToSql().CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("\"ProductId\""));
            Assert.That(sql, Does.Contain("\"Revenue\""));
            Assert.That(sql, Does.Contain("\"AverageRevenue\""));
            Assert.That(sql, Does.Match(
                @"(?is)WHERE\s+\(\s*q0\.\""Revenue\""\s*>\s*q0\.\""AverageRevenue\""\s*\)"));
        });
    }

    [Test]
    public void DefinitionExecutesWithProjectionAndFilter()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name"),
            Filter = QueryDefinitionJsonParser.ParseFilter("Name = 'Alice'"),
        };

        var records = _db.Query(definition).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void DefinitionExecutesFilterWithFieldReferenceOnRight()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name"),
            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    LogicalOperator.And,
                    new FieldDefinition("Age"),
                    BinaryOperator.GreaterThan,
                    new FieldDefinition("Id")))
        };

        var query = _db.Query(definition);
        var sql = query.ToSql();
        var records = query.ToList();

        Assert.That(sql.CommandText, Does.Contain("Age"));
        Assert.That(sql.CommandText, Does.Contain("Id"));
        Assert.That(records, Is.Not.Null);
    }

    [Test]
    public void DefinitionFiltersOnFunctionExpression()
    {
        var definition = new QueryDefinition("User")
        {

            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    null,
                    new FunctionFieldDefinition("Month", [new FieldDefinition("Birthday")]),
                    BinaryOperator.Equals,
                    new ConstFieldDefinition(3)))
        };

        var query = _db.Query(definition);
        var sql = query.ToSql();
        var records = query.ToList();
        var monthFunction = _functionProvider.GetFunction("Month");
        var expectedMonthExpression = monthFunction
            .GetSqlTemplate(_sqlDialect.Key)
            .Replace("{1}", "t0." + _sqlDialect.QuoteIdentifier("Birthday"), StringComparison.Ordinal);
        var normalizedSql = System.Text.RegularExpressions.Regex.Replace(
            sql.CommandText,
            @"\s+",
            string.Empty);
        var normalizedExpectedMonthExpression = System.Text.RegularExpressions.Regex.Replace(
            expectedMonthExpression,
            @"\s+",
            string.Empty);

        Assert.That(normalizedSql, Does.Contain(normalizedExpectedMonthExpression));
        Assert.That(records.Select(record => record["Name"]), Is.EqualTo(new object?[] { "Charlie" }));
    }

    [Test]
    public void DefinitionSupportsExtendedFilterOperators()
    {
        var filters = new (BinaryOperator Operation, IFieldDefinition? Right, string Sql)[]
        {
            (BinaryOperator.Between,
                new GroupFieldDefintion([new ConstFieldDefinition(18), new ConstFieldDefinition(65)]),
                "BETWEEN"),
            (BinaryOperator.NotBetween,
                new GroupFieldDefintion([new ConstFieldDefinition(18), new ConstFieldDefinition(65)]),
                "NOT BETWEEN"),
            (BinaryOperator.In,
                new GroupFieldDefintion([new ConstFieldDefinition("Alice"), new ConstFieldDefinition("Bob")]),
                " IN "),
            (BinaryOperator.NotIn,
                new GroupFieldDefintion([new ConstFieldDefinition("Alice"), new ConstFieldDefinition("Bob")]),
                "NOT IN"),
            (BinaryOperator.IsNull, null, "IS NULL"),
            (BinaryOperator.IsNotNull, null, "IS NOT NULL"),
            (BinaryOperator.NotLike, new ConstFieldDefinition("A%"), "NOT LIKE")
        };

        foreach (var (operation, right, sqlOperator) in filters)
        {
            var definition = new QueryDefinition("User")
            {

                Projections = QueryDefinitionJsonParser.ParseProjection("Name"),
                Filter = new ConditionDefinition(
                    LogicalOperator.And,
                    new ConditionStatementDefinition(
                        LogicalOperator.And,
                        new FieldDefinition("Name"),
                        operation,
                        right))
            };

            var sql = _db.Query(definition).ToSql();

            Assert.That(sql.CommandText, Does.Contain(sqlOperator), operation.ToString());
        }
    }

    [Test]
    public void DefinitionRejectsInvalidRangeAndEmptySetFilters()
    {
        var invalidRange = new QueryDefinition("User")
        {

            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    LogicalOperator.And,
                    new FieldDefinition("Age"),
                    BinaryOperator.Between,
                    new GroupFieldDefintion([new ConstFieldDefinition(18)])))
        };
        var emptySet = new QueryDefinition("User")
        {

            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    LogicalOperator.And,
                    new FieldDefinition("Name"),
                    BinaryOperator.In,
                    new GroupFieldDefintion([])))
        };

        Assert.Throws<FormatException>(() => _db.Query(invalidRange).ToSql());
        Assert.Throws<FormatException>(() => _db.Query(emptySet).ToSql());
    }

    [Test]
    public void RangeFilterAcceptsCommaSeparatedUiValue()
    {
        var definition = new QueryDefinition("User")
        {

            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    LogicalOperator.And,
                    new FieldDefinition("Id"),
                    BinaryOperator.Between,
                    new ConstFieldDefinition("100,200"))
                {
                    Parameterized = true
                })
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("BETWEEN"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { "100", "200" }));
    }

    [Test]
    public void RangeFilterAcceptsCommaSeparatedValues()
    {
        var definition = QueryDefinitionJsonParser.ParseFilter("Id between <100,200>");

        var sql = _db.Query(new QueryDefinition("User")
        {

            Filter = definition
        }).ToSql();

        Assert.That(sql.CommandText, Does.Contain("BETWEEN"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 100, 200 }));
    }

    [Test]
    public void DefinitionSupportsAggregateOnRootEntity()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, COUNT(Id) | InvoiceCount, SUM(Age) | Revenue, AVG(Age) | AverageOrderValue"),
            Filter = null
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("GROUP BY"));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bCOUNT\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bSUM\s*\("));
        Assert.That(sql.CommandText, Does.Match(@"(?i)\bAVG\s*\("));
        Assert.That(sql.CommandText, Does.Not.Match(@"(?i)\bSELECT\s+COUNT\s*\(\s*\*\s*\)"));
        Assert.That(sql.CommandText, Does.Not.Contain("Average("));
        Assert.That(records, Is.Not.Empty);
        Assert.That(records[0]["InvoiceCount"], Is.EqualTo(1L));
    }

    [Test]
    public void DefinitionSupportsBracketAggregateFunctions()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, COUNT(Id) | InvoiceCount, SUM[Age] | TotalAge, AVG[Age] | AverageAge"),
            Filter = null
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain("GROUP BY"));
        Assert.That(sql, Does.Match(@"(?i)\bCOUNT\s*\("));
        Assert.That(sql, Does.Match(@"(?i)\bSUM\s*\("));
        Assert.That(sql, Does.Match(@"(?i)\bAVG\s*\("));
    }

    [Test]
    public void DefinitionRejectsWildcardAndEmptyCountArguments()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, Count[*] | WildcardCount, Count[] | EmptyCount"),
            Filter = null
        };

        Assert.That(
            () => _db.Query(definition).ToSql(),
            Throws.TypeOf<UnauthorizedAccessException>()
                .With.Message.Contain("COUNT(*)"));
    }

    [Test]
    public void DefinitionSortsByCollectionAggregate()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, Sum(Members.Age) | TotalMemberAge"),
            Sorts = [new SortDefinition(new FunctionFieldDefinition("Sum", [new FieldDefinition("Members.Age")]))]
        };

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.CommandText, Does.Contain("ORDER BY SUM ("));
        Assert.That(sql.CommandText, Does.Not.Contain("ORDER BY (\n    SELECT"));
        Assert.That(records.Select(record => record["Name"]),
            Is.EqualTo(new object?[] { "Charlie", "Eve", "Frank", "Bob", "David", "Alice" }));
    }

    [Test]
    public void DefinitionSortsByProjectedAlias()
    {
        var definition = new QueryDefinition("User")
        {

            Projections = QueryDefinitionJsonParser.ParseProjection("Name, Count(Id) | TotalCount"),
            Sorts = [new SortDefinition(new FieldDefinition("TotalCount"), QuerySortDirection.Descending)]
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain(
            "ORDER BY " + _sqlDialect.QuoteIdentifier("TotalCount") + " DESC"));
        Assert.That(sql, Does.Not.Contain("ORDER BY COUNT"));
    }

    [Test]
    public void SortDefinitionParserSplitsTopLevelExpressionsAndSupportsDefaultDirection()
    {
        var sorts = QueryDefinitionJsonParser.ParseSorts("Sum(Age, 1) DESC, Name");

        Assert.That(sorts, Has.Count.EqualTo(2));
        Assert.That(sorts[0].Field, Is.TypeOf<FunctionFieldDefinition>());
        Assert.That(sorts[0].Direction, Is.EqualTo(QuerySortDirection.Descending));
        Assert.That(((FieldDefinition)sorts[1].Field).FieldName, Is.EqualTo("Name"));
        Assert.That(sorts[1].Direction, Is.EqualTo(QuerySortDirection.Ascending));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryDefinition("User")
        {

            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionDefinition(
                    LogicalOperator.Or,
                    new ConditionStatementDefinition(
                        LogicalOperator.Or,
                        new FieldDefinition("Name"),
                        BinaryOperator.Equals,
                        new ConstFieldDefinition("Alice")),
                    new ConditionStatementDefinition(
                        LogicalOperator.Or,
                        new FieldDefinition("Age"),
                        BinaryOperator.GreaterThan,
                        new ConstFieldDefinition(18))),
                new ConditionStatementDefinition(
                    LogicalOperator.And,
                    new FieldDefinition("Name"),
                    BinaryOperator.Equals,
                    new ConstFieldDefinition("Bob")))
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("OR"));
        Assert.That(sql.CommandText, Does.Contain("AND"));
        Assert.That(sql.CommandText, Does.Not.Contain("= 12"));
    }

    [Test]
    public void DefinitionRejectsInvalidPaging()
    {
        var definition = new QueryDefinition("User")
        {

            Take = -1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _db.Query(definition));
    }

    [Test]
    public void DefinitionJsonDeserializesRecursiveFilterValues()
    {
        const string json = """
            {
              "entity": "User",
              "filter": {
                "logicalOperation": 0,
                "filters": [{
                  "logicalOperator": 0,
                  "field": { "kind": "field", "field": "Name" },
                  "operation": 0,
                  "right": { "kind": "value", "value": "Alice" },
                  "parameterized": false
                }]
              }
            }
            """;

        var definition = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(
            json,
            QueryDefinitionJsonParser.CreateSerializerOptions())!;

        Assert.That(definition.Filter, Is.TypeOf<ConditionDefinition>());
        var filter = definition.Filter!.Filters.Single() as ConditionStatementDefinition;
        Assert.That(filter, Is.Not.Null);
        Assert.That(filter!.Left, Is.TypeOf<FieldDefinition>());
        Assert.That(((FieldDefinition)filter.Left).FieldName, Is.EqualTo("Name"));
        Assert.That(filter.Right, Is.TypeOf<ConstFieldDefinition>());
        Assert.That(((ConstFieldDefinition)filter.Right!).Value, Is.EqualTo("Alice"));

        var options = QueryDefinitionJsonParser.CreateSerializerOptions();
        var serialized = System.Text.Json.JsonSerializer.Serialize(definition, options);
        Assert.That(serialized, Does.Contain("\"left\":{\"kind\":\"field\",\"field\":\"Name\"}"));
        Assert.That(serialized, Does.Contain("\"right\":{\"kind\":\"value\",\"value\":\"Alice\"}"));
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(serialized, options)!;
        Assert.That(((FieldDefinition)((ConditionStatementDefinition)roundTrip.Filter!.Filters.Single()).Left).FieldName, Is.EqualTo("Name"));
    }

}
