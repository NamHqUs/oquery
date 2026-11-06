# OQuery C# fluent syntax

This document describes the C# API exposed by the OQuery engine. It is based
on `OQueryEngine`, `FluentQueryExtensions`, `IQueryParameter`, and the fluent
API unit tests. The C# API builds the same logical query model as OQuery text;
the engine remains responsible for metadata binding, SQL generation,
parameters, dialect differences, and execution.

## 1. Create an engine and select an entity

`OQueryEngine.Query` creates a query rooted at a logical metadata entity:

```csharp
using var db = new OQueryEngine(connection, metadata);

var products = db.Query("Products");
```

The entity name is logical. Do not use a physical schema-qualified table name
in the fluent API. The metadata provider maps the logical entity to its
physical table.

`OQueryEngine.QueryOqr` is the C# bridge for a complete OQuery document. It is
important for named contexts and context dependencies:

```csharp
var employees = db.QueryOqr("""
    EmployeeTree is Employees:
        FirstName ManagerName,
        member.FirstName MemberName
      INNER JOIN EmployeeTree member on Id = member.ManagerId
    """);
```

Use `Recursive(relationName, projection)` for a self-referencing collection
relation. The engine infers the root rows, recursive step, and generated CTE
from metadata:

```csharp
var employees = db.Query("Employees")
    .Recursive("Members", (manager, members) => new
    {
        ManagerName = manager["FirstName"],
        MemberName = members["FirstName"],
        TotalMembers = members["Id"].Count()
    })
    .ToList();
```

The relation must be a collection pointing back to the source entity and must
have one key pair. The projection receives a manager scope and a members
scope. Collection aggregates use the same field syntax as scalar expressions:

```csharp
e["Members"].Count();
e["Members.Age"].Sum();
```

Calling an aggregate on a scalar field is rejected during query translation.
The relation name is the only recursive implementation detail exposed to the
caller.

## 2. Projection

`Select` accepts field-path strings, a dictionary of output aliases to
expressions, or an expression that returns that dictionary:

```csharp
var orders = db.Query("Orders")
    .Select("Id", "OrderDate", "User.FirstName", "User.LastName")
    .ToList();

var named = db.Query("User")
    .Select(new Dictionary<string, object?>
    {
        ["DisplayName"] = "Name",
        ["ManagerName"] = "Manager.Name"
    })
    .ToList();
```

Field paths use logical metadata names. Relation traversal is written with
dots. Collection aggregates and supported SQL functions can be written in the
same expression string:

```csharp
var totals = db.Query("User")
    .Select("Name", "Members.Count(Id)", "Members.Sum(Age)")
    .ToList();
```

The expression overload is useful when a filter also references a projected
alias:

```csharp
var adults = db.Query("User")
    .Select(e => new Dictionary<string, object?>
    {
        ["Name"] = e["Name"],
        ["ManagerName"] = e["Manager.Name"]
    })
    .Where(e => e["ManagerName"] == "Alice")
    .ToList();
```

## 3. Filters

`Where` accepts `Expression<Func<IQueryParameter, bool>>`. Fields are accessed
with the indexer, and related collections can use `Query` with `Any` or
`Count`:

```csharp
var results = db.Query("User")
    .Where(e => e["Age"] > 32 && e["IsMale"] == true)
    .Where(e => e.Query("Members").Any(member => member["IsMale"] == e["IsMale"]))
    .ToList();
```

`QueryValue` supports equality and comparison operators plus `Like`,
`Between`, `In`, `IsNull`, and their corresponding negative forms. Values are
parameters; application values should not be concatenated into expression
strings.

## 4. Ordering and paging

Ordering uses logical field names and can be chained:

```csharp
var page = db.Query("User")
    .Select("Id", "Name")
    .OrderBy("Name")
    .ThenByDescending("Id")
    .Skip(20)
    .TakeQuery(10)
    .ToList();
```

`Skip` and `TakeQuery` add paging to the query. `Take` is an execution helper
that returns a list immediately, so use `TakeQuery` when composing a query.

## 5. Execution and SQL inspection

Use the execution extensions for records, totals, asynchronous execution, and
diagnostic SQL:

```csharp
var records = query.ToList();
var result = query.ToResult();
var (rows, totalRecords) = query.ToListWithTotalRecords();
var sql = query.ToSql();
```

`ToSql()` returns the generated SQL and its parameters. It is a translation
inspection API, not a replacement for the engine's execution and
authorization path.

## 6. Mapping the four home-page samples

The first two samples use the fluent extensions directly:

```csharp
var products = db.Query("Products")
    .Select("*")
    .ToList();

var orders = db.Query("Orders")
    .Select("Id", "OrderDate", "User.FirstName", "User.LastName")
    .ToList();
```

The recursive hierarchy and product-sales examples use the fluent API:

```csharp
var employees = db.Query("Employees")
    .Recursive("Members", (manager, members) => new
    {
        ManagerName = manager["FirstName"],
        MemberName = members["FirstName"],
        TotalMembers = members["Id"].Count()
    })
    .ToList();
var sales = db.Query("OrderItems")
    .Select(item => new
    {
        ProductName = "Product.Name",
        TotalRevenue = "SUM(Quantity * UnitPrice)",
        Average = "AverageOver(TotalRevenue)"
    })
    .Where(item => item["TotalRevenue"] > item["Average"])
    .ToList();
```

The recursive relation and projection aliases are translated into the same
recursive CTE and projection-layer shapes used by OQuery text.

## 7. API boundaries

- Use logical entity, field, and relation names.
- Keep physical table/schema names in metadata and generated SQL.
- Use `Recursive(relationName)` for self-referencing collection relations.
- Use `QueryOqr` or `QueryDefinition` for named contexts and context
  dependencies.
- Execute through the query provider so dialect translation, parameters,
  projection dependencies, and metadata authorization remain centralized.
