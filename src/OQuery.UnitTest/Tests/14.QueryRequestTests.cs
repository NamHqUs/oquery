using OQuery.QueryDefinitions.Parsers;

namespace OQuery.Test.Tests;

[TestFixture]
public class _14_QueryRequestTests : TestBase
{
    [Test]
    public void DefinitionBuildsParameterizedSql()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, Manager.Name | ManagerName"),
            Filter = QueryDefinitionParser.ParseFilter("Age > 18"),
            Sorts =
            [
                new SortDefinition(new FieldDefinition("Name"))
            ],
            Page = 1,
            PageSize = 10
        };

        var sql = _db.Query(definition).ToSql();

        Assert.That(sql.CommandText, Does.Contain("SELECT"));
        Assert.That(sql.CommandText, Does.Contain("ORDER BY"));
        Assert.That(sql.CommandText, Does.Contain(_sqlDialect.RenderPaging(0, 10, hasOrderBy: true)));
        Assert.That(sql.CommandText, Does.Contain("@p0"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 18 }));
    }

    [TestCase(null)]
    [TestCase(0)]
    public void DefinitionDoesNotPageWhenPageSizeIsUnsetOrZero(int? pageSize)
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name"),
            Page = 1,
            PageSize = pageSize
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Not.Contain("OFFSET"));
        Assert.That(sql, Does.Not.Contain("FETCH NEXT"));
    }

    [Test]
    public void AggregateFilterUsesHavingInsteadOfScalarSubquery()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, AVG(Age) | AverageAge"),
            Filter = QueryDefinitionParser.ParseFilter("AVG(Age) > 12"),
            PageSize = 50
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
    public void DefinitionExecutesWithProjectionAndFilter()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name"),
            Filter = QueryDefinitionParser.ParseFilter("Name = 'Alice'"),
        };

        var records = _db.Query(definition).ToList();

        Assert.That(records, Has.Count.EqualTo(1));
        Assert.That(records[0]["Name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void DefinitionExecutesFilterWithFieldReferenceOnRight()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name"),
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
        var definition = new QueryDefinition
        {
            Entity = "User",
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
            var definition = new QueryDefinition
            {
                Entity = "User",
                Projection = QueryDefinitionParser.ParseProjection("Name"),
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
        var invalidRange = new QueryDefinition
        {
            Entity = "User",
            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionStatementDefinition(
                    LogicalOperator.And,
                    new FieldDefinition("Age"),
                    BinaryOperator.Between,
                    new GroupFieldDefintion([new ConstFieldDefinition(18)])))
        };
        var emptySet = new QueryDefinition
        {
            Entity = "User",
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
        var definition = new QueryDefinition
        {
            Entity = "User",
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
        var definition = QueryDefinitionParser.ParseFilter("Id between <100,200>");

        var sql = _db.Query(new QueryDefinition
        {
            Entity = "User",
            Filter = definition
        }).ToSql();

        Assert.That(sql.CommandText, Does.Contain("BETWEEN"));
        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { 100, 200 }));
    }

    [Test]
    public void MultiValueFilterRoundTripsThroughJson()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Filter = QueryDefinitionParser.ParseFilter("Id between <100,200>")
        };
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var serialized = System.Text.Json.JsonSerializer.Serialize(definition, options);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(serialized, options)!;
        var filter = (ConditionStatementDefinition)roundTrip.Filter!.Filters.Single();
        var values = ((GroupFieldDefintion)filter.Right!).Fields
            .Cast<ConstFieldDefinition>()
            .Select(value => value.Value);

        Assert.That(values, Is.EqualTo(new object?[] { 100, 200 }));
    }

    [Test]
    public void DefinitionSupportsAggregateOnRootEntity()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, COUNT(Id) | InvoiceCount, SUM(Age) | Revenue, AVG(Age) | AverageOrderValue"),
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
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, COUNT(Id) | InvoiceCount, SUM[Age] | TotalAge, AVG[Age] | AverageAge"),
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
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, Count[*] | WildcardCount, Count[] | EmptyCount"),
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
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, Sum(Members.Age) | TotalMemberAge"),
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
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Name, Count(Id) | TotalCount"),
            Sorts = [new SortDefinition(new FieldDefinition("TotalCount"), QuerySortDirection.Descending)]
        };

        var sql = _db.Query(definition).ToSql().CommandText;

        Assert.That(sql, Does.Contain(
            "ORDER BY " + _sqlDialect.QuoteIdentifier("TotalCount") + " DESC"));
        Assert.That(sql, Does.Not.Contain("ORDER BY COUNT"));
    }

    [Test]
    public void YmlDefinitionSupportsSortExpressions()
    {
        const string yml = """
            Entity: User
            Sorts: Sum(Age) DESC
            """;

        var definition = QueryDefinitionParser.ParseFromYml(yml);
        var sort = definition.Sorts.Single();
        var sql = _db.Query(definition).ToSql().CommandText;
        var serialized = System.Text.Json.JsonSerializer.Serialize(
            definition,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var roundTrippedSort = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(
            serialized,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!
            .Sorts.Single();

        Assert.That(sort.Field, Is.TypeOf<FunctionFieldDefinition>());
        Assert.That(sort.Direction, Is.EqualTo(QuerySortDirection.Descending));
        Assert.That(roundTrippedSort.Field, Is.TypeOf<FunctionFieldDefinition>());
        Assert.That(roundTrippedSort.Direction, Is.EqualTo(QuerySortDirection.Descending));
        Assert.That(sql, Does.Contain("ORDER BY SUM ("));
        Assert.That(sql, Does.Contain("DESC"));
    }

    [Test]
    public void SortDefinitionParserSplitsTopLevelExpressionsAndSupportsDefaultDirection()
    {
        var sorts = QueryDefinitionParser.ParseSorts("Sum(Age, 1) DESC, Name");

        Assert.That(sorts, Has.Count.EqualTo(2));
        Assert.That(sorts[0].Field, Is.TypeOf<FunctionFieldDefinition>());
        Assert.That(sorts[0].Direction, Is.EqualTo(QuerySortDirection.Descending));
        Assert.That(((FieldDefinition)sorts[1].Field).FieldName, Is.EqualTo("Name"));
        Assert.That(sorts[1].Direction, Is.EqualTo(QuerySortDirection.Ascending));
    }

    [Test]
    public void DefinitionConnectsStructuredFiltersWithLogicalOperators()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
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
        var definition = new QueryDefinition
        {
            Entity = "User",
            Page = 0,
            PageSize = 10
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _db.Query(definition));
    }

    [Test]
    public void DefinitionFormatsAsReadableText()
    {
        var definition = new QueryDefinition
        {
            Entity = "User",
            Projection = QueryDefinitionParser.ParseProjection("Id, Name"),
            Filter = new ConditionDefinition(
                LogicalOperator.And,
                new ConditionDefinition(
                    LogicalOperator.And,
                    new ConditionStatementDefinition(
                        LogicalOperator.And,
                        new FieldDefinition("Id"),
                        BinaryOperator.GreaterThan,
                        new ConstFieldDefinition(3)),
                    new ConditionStatementDefinition(
                        LogicalOperator.And,
                        new FieldDefinition("Invoices.Id"),
                        BinaryOperator.LessThan,
                        new ConstFieldDefinition(3))),
                new ConditionDefinition(
                    LogicalOperator.Or,
                    new ConditionStatementDefinition(
                        LogicalOperator.Or,
                        new FieldDefinition("Name"),
                        BinaryOperator.Equals,
                        new ConstFieldDefinition("abc")),
                    new ConditionStatementDefinition(
                        LogicalOperator.Or,
                        new FieldDefinition("Age"),
                        BinaryOperator.GreaterThan,
                        new ConstFieldDefinition(18)))),
            Sorts = [new SortDefinition(new FieldDefinition("Name"))],
            Page = 1,
            PageSize = 25
        };

        var expected = string.Join(Environment.NewLine,
            "Entity: User",
            "Projection:",
            "  - Id",
            "  - Name",
            "Filter: (Id > <3> AND Invoices.Id < <3>) AND (Name = <abc> OR Age > <18>)",
            "Sorts: Name ASC",
            "Page: 1",
            "PageSize: 25");

        Assert.That(definition.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void DefinitionFormatsEmptyValuesAsBlank()
    {
        var definition = new QueryDefinition { Entity = "User" };

        var expected = string.Join(Environment.NewLine,
            "Entity: User");

        Assert.That(definition.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void DefinitionYamlOmitsSavedRequestMetadata()
    {
        const string json = """
            {
              "entity": "Customers",
              "name": "Customer segments",
              "fields": [
                { "field": { "kind": "field", "field": "Phone" }, "alias": null },
                { "field": { "kind": "field", "field": "Region" }, "alias": null },
                { "field": { "kind": "field", "field": "Segment" }, "alias": null }
              ],
              "projectionRows": [["Phone", "Region"], ["Segment"]]
            }
            """;

        var definition = QueryDefinitionParser.Parse(json);
        var yml = definition.ToYml();

        Assert.That(yml, Does.Not.Contain("Name:"));
        Assert.That(yml, Does.Not.Contain("ProjectionRows:"));
        Assert.That(yml, Does.Contain("Projection:"));
    }

    [Test]
    public void JsonDefinitionIgnoresNewLineProjectionMarkers()
    {
        const string json = """
            {
              "entity": "User",
              "projection": [
                { "field": { "kind": "field", "field": "Id" }, "alias": null },
                { "field": { "kind": "field", "field": "" }, "alias": "NEW_LINE" },
                { "field": { "kind": "field", "field": "Name" }, "alias": null }
              ]
            }
            """;

        var definition = QueryDefinitionParser.Parse(json);

        Assert.That(definition.Projection.Select(projection => projection.ToText()),
            Is.EqualTo(new[] { "Id", "Name" }));
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
                  "value": "Alice",
                  "parameterized": false
                }]
              }
            }
            """;

        var definition = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.That(definition.Filter, Is.TypeOf<ConditionDefinition>());
        var filter = definition.Filter!.Filters.Single() as ConditionStatementDefinition;
        Assert.That(filter, Is.Not.Null);
        Assert.That(filter!.Left, Is.TypeOf<FieldDefinition>());
        Assert.That(((FieldDefinition)filter.Left).FieldName, Is.EqualTo("Name"));
        Assert.That(filter.Right, Is.TypeOf<ConstFieldDefinition>());
        Assert.That(((ConstFieldDefinition)filter.Right!).Value, Is.EqualTo("Alice"));

        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var serialized = System.Text.Json.JsonSerializer.Serialize(definition, options);
        Assert.That(serialized, Does.Contain("\"left\":{\"kind\":\"field\",\"field\":\"Name\"}"));
        Assert.That(serialized, Does.Contain("\"right\":{\"kind\":\"value\",\"value\":\"Alice\"}"));
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<QueryDefinition>(serialized, options)!;
        Assert.That(((FieldDefinition)((ConditionStatementDefinition)roundTrip.Filter!.Filters.Single()).Left).FieldName, Is.EqualTo("Name"));
    }

    [Test]
    public void DefinitionTextUsesFieldDtoLiteralSyntax()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: User
            Filter: "Name = <Alice Smith>"
            """);

        Assert.That(definition.ToString(), Does.Contain("Filter: Name = <Alice Smith>"));
    }

    [Test]
    public void YmlDefinitionSupportsTextFilters()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: User
            Filter: Name = Alice
            """);

        Assert.That(((FieldDefinition)((ConditionStatementDefinition)definition.Filter!.Filters.Single()).Left).FieldName, Is.EqualTo("Name"));
    }

    [Test]
    public void YmlDefinitionSupportsAliasesLikeGroupingSortingAndPaging()
    {
        const string yml = """
            Entity: User
            Projection:
              - Name
              - Manager.Name | Manager-Name
            Filter: "[Name] like 'Dav%' && ([Manager.Name] == 'Alice' || [Members.COUNT(Id)] < 12)"
            Sorts: Manager.Name DESC
            Page: 1
            PageSize: 50
            """;

        var sql = _db.QueryYml(yml).ToSql();

        Assert.That(sql.Parameters.Select(parameter => parameter.Value),
            Is.EquivalentTo(new object?[] { "Dav%", "Alice", 12 }));

        var data = _db.QueryYml(yml).ToList();

        string[] expectedValues = ["David - Bob"];
        var actualValues = data.Select(e => $"{e["Name"]} - {e["Manager-Name"] ?? "NULL"}").ToArray();
        Assert.That(actualValues, Is.EquivalentTo(expectedValues));
    }

    [Test]
    public void YmlDefinitionRejectsInvalidFilter()
    {
        const string yml = """
            Entity: User
            Filter: Name like
            """;

        Assert.Throws<FormatException>(() => QueryDefinitionParser.ParseFromYml(yml));
        Assert.Throws<FormatException>(() =>
            QueryDefinitionParser.ParseFromYml("""
                Entity: User
                Filter: Age > (12 + OtherAge)
                """));
    }

    [Test]
    public void YmlDefinitionAcceptsUnquotedStringFilterValues()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: User
            Filter: Name == Alice
            """);

        var sql = _db.Query(definition).ToSql();
        var records = _db.Query(definition).ToList();

        Assert.That(sql.Parameters.Select(parameter => parameter.Value), Is.EqualTo(new object?[] { "Alice" }));
        Assert.That(records.Select(record => record["Name"]), Is.EqualTo(new object?[] { "Alice" }));
    }

    [Test]
    public void YmlDefinitionParsesEscapedSingleQuotesInFilterValues()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: User
            Filter: "Name == 'O''Neil'"
            """);
        var filter = (ConditionStatementDefinition)definition.Filter!.Filters[0];

        Assert.That(((ConstFieldDefinition)filter.Right!).Value, Is.EqualTo("O'Neil"));
    }

    [Test]
    public void FilterTextRoundTripsDtoOperandsAndLogicalGroups()
    {
        var definition = QueryDefinitionParser.ParseFromYml("""
            Entity: User
            Filter: >-
              Name = "Alice Smith" AND (Age >= 18 OR Name = [Manager.Name])
            """);
        var text = definition.Filter!.ToText();
        var roundTrip = QueryDefinitionParser.ParseFromYml($"""
            Entity: User
            Filter: >-
              {text}
            """);
        var outerFilters = ((ConditionDefinition)roundTrip.Filter!).Filters;
        var fieldReferenceFilter = (ConditionStatementDefinition)((ConditionDefinition)outerFilters[1]).Filters[1];

        Assert.That(text, Is.EqualTo(
            "Name = <Alice Smith> AND (Age >= <18> OR Name = Manager.Name)"));
        Assert.That(((ConstFieldDefinition)((ConditionStatementDefinition)outerFilters[0]).Right!).Value, Is.EqualTo("Alice Smith"));
        Assert.That(fieldReferenceFilter.Right, Is.TypeOf<FieldDefinition>());
        Assert.That(((FieldDefinition)fieldReferenceFilter.Right!).FieldName, Is.EqualTo("Manager.Name"));
        Assert.That(roundTrip.Filter!.ToText(), Is.EqualTo(text));
    }

    [Test]
    public void FilterTextParserDelegatesAngleValuesToFieldParser()
    {
        var cases = new (string Text, object? Value)[]
        {
            ("Age = <18>", 18),
            ("Age = <18.5>", 18.5m),
            ("Active = <true>", true),
            ("Name = <Alice Smith>", "Alice Smith"),
            ("Name = <>", null)
        };

        foreach (var (text, value) in cases)
        {
            var definition = QueryDefinitionParser.ParseFromYml($"""
                    Entity: User
                    Filter: "{text}"
                """);
            var filter = (ConditionStatementDefinition)definition.Filter!.Filters.Single();

            Assert.That(((ConstFieldDefinition)filter.Right!).Value, Is.EqualTo(value), text);
        }

        var nullRightDefinition = QueryDefinitionParser.ParseFromYml("""
            Entity: User
            Filter: Name = <NULL>
            """);
        Assert.That(
            ((ConditionStatementDefinition)nullRightDefinition.Filter!.Filters.Single()).Right,
            Is.Null);
    }

}
