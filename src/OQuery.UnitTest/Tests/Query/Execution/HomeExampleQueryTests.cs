using OQuery.UnitTest;

namespace OQuery.UnitTest.Tests.Query.Execution;

[TestFixture]
public sealed class HomeExampleQueryTests : TestBase
{
    [Test]
    public void ProductsExampleBuildsASelectAllQuery()
    {
        var sql = _db.Query("Products")
            .Select("*")
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("SELECT"));
        Assert.That(sql, Does.Contain("\"Products\""));
    }

    [Test]
    public void OrdersExampleBuildsRelationTraversalProjection()
    {
        var sql = _db.Query("Orders")
            .Select("Id", "OrderDate", "User.FirstName", "User.LastName")
            .ToSql()
            .CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("\"Orders\""));
            Assert.That(sql, Does.Contain("\"OrderDate\""));
            Assert.That(sql, Does.Contain("\"FirstName\""));
            Assert.That(sql, Does.Contain("\"LastName\""));
            Assert.That(sql, Does.Contain("JOIN"));
        });
    }

    [Test]
    public void ProductSalesExampleBuildsWindowProjectionAndAliasFilter()
    {
        var query = _db.Query("OrderItems")
            .Select(item => new
            {
                ProductName = "Product.Name",
                TotalRevenue = "SUM(Quantity * UnitPrice)",
                Average = "AverageOver(TotalRevenue)"
            })
            .Where(item => item["TotalRevenue"] > item["Average"]);

        var sql = query.ToSql().CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Match(
                @"(?is)AVG\s*\(\s*q0\.\""TotalRevenue\""\s*\)\s+OVER\s*\(\s*\)\s+\""Average\"""));
            Assert.That(sql, Does.Match(
                @"(?is)SUM\s*\(\s*.*\""Quantity\""[\s\S]*\*\s*.*\""UnitPrice\"""));
            Assert.That(sql, Does.Match(
                @"(?is)WHERE\s*\(\s*q1\.\""TotalRevenue\""\s*>\s*q1\.\""Average\""\s*\)"));
        });
    }

    [Test]
    public void CollectionFieldAggregatesUseUnifiedFieldSyntax()
    {
        var sql = _db.Query("User")
            .Select(e => new
            {
                TotalMembers = e["Members"].Count(),
                TotalAge = e["Members"].Sum("Age")
            })
            .ToSql()
            .CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("COUNT"));
            Assert.That(sql, Does.Contain("SUM"));
            Assert.That(sql, Does.Contain("\"ManagerId\""));
            Assert.That(sql, Does.Contain("GROUP BY"));
        });
    }

    [Test]
    public void SelectSupportsAnonymousProjectionWithCollectionAggregate()
    {
        var sql = _db.Query("User")
            .Select(e => new
            {
                Name = e["Name"],
                TotalMembers = e["Members"].Count()
            })
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("\"TotalMembers\""));
        Assert.That(sql, Does.Contain("COUNT"));
    }

    [Test]
    public void CollectionAggregateAcceptsTextExpressionArgument()
    {
        var sql = _db.Query("User")
            .Select(e => new
            {
                TotalAge = e["Members"].Sum("Age + 1")
            })
            .ToSql()
            .CommandText;

        Assert.That(sql, Does.Contain("SUM"));
    }

    [Test]
    public void ScalarFieldAggregateFailsDuringTranslation()
    {
        Assert.That(
            () => _db.Query("User")
                .Select(e => new
                {
                    Invalid = e["Name"].Count()
                })
                .ToSql(),
            Throws.TypeOf<FormatException>()
                .With.Message.Contains("requires a collection relation"));

        Assert.That(
            () => _db.Query("User")
                .Select(e => new
                {
                    Invalid = e["Members.Age"].Sum()
                })
                .ToSql(),
            Throws.TypeOf<FormatException>()
                .With.Message.Contains("requires a collection relation"));
    }

    [Test]
    public void EmployeeTreeExampleBuildsRecursiveQuery()
    {
        var sql = _db.Query("Employees")
            .Recursive("Members", (manager, members) => new
            {
                ManagerName = manager["FirstName"],
                MemberName = members["FirstName"],
                TotalMembers = members["Id"].Count()
            })
            .ToSql()
            .CommandText;

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Match(@"(?is)WITH\s+RECURSIVE"));
            Assert.That(sql, Does.Contain("\"EmployeesTree\""));
            Assert.That(sql, Does.Contain("INNER JOIN \"EmployeesTree\""));
            Assert.That(sql, Does.Contain("COUNT (member.\"Id\") OVER"));
        });
    }
}
