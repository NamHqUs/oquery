# OQuery syntax guide

This document is the authoring reference for generating OQuery statements.
Use semantic entity and field names from metadata. Do not copy physical SQL
schema names into OQuery.

## 1. OQuery is semantic, not SQL text

OQuery describes the business query shape. The SQL compiler adds physical
table names, schema names, aliases, quoting, parameters, joins, grouping, and
provider-specific syntax.

Use this:

```text
Products: Id, Name
```

Do not use this:

```text
sa.Products: Id, Name
```

The physical `sa` schema belongs to generated SQL, not OQuery syntax.

## 2. Query header

The basic form is:

```text
Entity: Projection
```

Example:

```text
Products: Id, Name, Category
```

An entity-only query selects the metadata fields:

```text
Products
```

Projection aliases use either whitespace or `as`:

```text
Products: Id ProductId, Name as ProductName
```

Use aliases when an expression should have a stable result-column name:

```text
Products: SUM(OrderItems.Quantity) UnitsSold
```

## 3. Clauses

Clauses are indented under the query header:

```text
Products: Id, Name
  Where Category = <Electronics>
  Order Name ASC
  Skip 20
  Take 25
```

The parser also accepts a clause immediately after a query header without
indentation. Indentation is still recommended for readability.

Supported query clauses include:

- `Where`
- `Order`
- `Skip`
- `Take`
- `Inner Join`
- `Left Join`
- `Right Join`
- `Full Join`

Keywords are case-insensitive. Use the canonical capitalization shown above
for readable saved queries.

Values use angle brackets:

```text
Where Status = <Completed>
Where Quantity > <10>
Where IsActive = <true>
```

## 4. Fields and relationships

Use metadata field names:

```text
Products: Id, Name, UnitPrice
```

Use relation paths for ordinary metadata relationships:

```text
Orders: Id, Customer.Name CustomerName
```

Collection relationships can be used in aggregates:

```text
Products: Id,
    Name,
    SUM(OrderItems.Quantity) UnitsSold
```

The compiler resolves the relationship and creates the required SQL join.

## 5. Reusable contexts

A context is a named query block:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue
```

The general form is:

```text
ContextName is SourceEntity: Projection
```

Contexts are useful for:

- per-parent aggregation;
- dependent aggregation;
- reusable calculated data;
- ranking and paging before an outer query;
- representing a derived table or CTE.

The source can be another context declared earlier:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue

ProductMaximum is ProductSales:
    MAX(TotalRevenue) MaximumRevenue
```

Declare dependent contexts in dependency order.

## 6. Joining contexts

Join a context from the query that consumes it:

```text
Products: Id,
    Name,
    ProductSales.TotalRevenue TotalRevenue
  Inner Join ProductSales On Id = ProductSales.ProductId
```

Use an explicit condition when the context has one row per parent key:

```text
Customers: Id,
    Name,
    CustomerOrders.OrderCount OrderCount
  Left Join CustomerOrders On Id = CustomerOrders.CustomerId
```

A context relation without a condition declares the context as available to the
query. It must not be used to create a row-multiplying join unless the
generated SQL join condition is explicit.

## 7. Aggregation and grouping

An aggregate function is grouped when the same projection contains both:

1. an aggregate expression; and
2. one or more non-aggregate fields.

Example:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue
```

This means one result row per `ProductId` and generates the equivalent of:

```sql
SELECT ProductId, SUM(Quantity * UnitPrice) AS TotalRevenue
FROM OrderItems
GROUP BY ProductId
```

The standard grouping aggregates are:

```text
COUNT(expression)
SUM(expression)
AVG(expression)
MAX(expression)
MIN(expression)
```

An aggregate-only context returns one aggregate row:

```text
AverageRevenue is ProductSales:
    AVG(TotalRevenue) AverageTotalRevenue
```

Do not use an ordinary aggregate in a detail projection when the desired
behavior is a scalar value repeated for each row. Use an explicit `*Over`
function instead.

## 8. Aggregate filters and scalar context aggregates

Aggregate predicates on the current query are routed to `HAVING`:

```text
Products: Id,
    Name,
    COUNT(OrderItems.Id) OrderCount
  Where COUNT(OrderItems.Id) > <5>
```

An aggregate over a context-qualified field in a filter is a scalar subquery
over that context:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue

Products: Id,
    Name,
    ProductSales.TotalRevenue TotalRevenue
  Inner Join ProductSales On Id = ProductSales.ProductId
  Where ProductSales.TotalRevenue > AVG(ProductSales.TotalRevenue)
```

The filter represents:

```sql
WHERE ProductSales.TotalRevenue >
(
    SELECT AVG(TotalRevenue)
    FROM ProductSales
)
```

This is the supported way to compare a grouped value with an aggregate over
the complete context without writing SQL manually.

Do not generate an ordinary `AVG(...)`, `SUM(...)`, or `MAX(...)` in a detail
projection when a scalar subquery would be required. Use an aggregate-over
window function as described next.

## 9. Aggregate-over window functions

Aggregate-over functions return scalar values as columns while preserving the
current result rows. They do not collapse rows and do not create a `GROUP BY`.

Supported functions:

```text
CountOver(expression, optionalPartition)
SumOver(expression, optionalPartition)
AverageOver(expression, optionalPartition)
MaxOver(expression, optionalPartition)
MinOver(expression, optionalPartition)
```

The first argument is required. The second argument is optional:

```text
CountOver(Member.Id)
```

generates:

```sql
COUNT(Member.Id) OVER ()
```

With a partition:

```text
CountOver(Member.Id, Manager.Id)
```

generates:

```sql
COUNT(Member.Id) OVER (PARTITION BY Manager.Id)
```

Examples:

```text
Products: Id,
    Name,
    ProductSales.TotalRevenue TotalRevenue,
    AverageOver(ProductSales.TotalRevenue) OverallAverageRevenue,
    SumOver(ProductSales.TotalRevenue) OverallRevenue,
    CountOver(ProductSales.ProductId) ProductCount,
    MaxOver(ProductSales.TotalRevenue) MaximumRevenue,
    MinOver(ProductSales.TotalRevenue) MinimumRevenue
  Inner Join ProductSales On Id = ProductSales.ProductId
```

Partitioned examples:

```text
Employees: Id,
    DepartmentId,
    Salary,
    AverageOver(Salary, DepartmentId) DepartmentAverageSalary,
    MaxOver(Salary, DepartmentId) DepartmentMaximumSalary
```

`*Over` is explicit syntax for a window scalar. It must not be classified as
a grouping aggregate and must not move a row predicate to `HAVING`.

## 10. Projection alias dependencies

Projection aliases can be referenced by later projection expressions and
predicates:

```text
OrderItems:
    ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue,
    AverageOver(TotalRevenue) AverageRevenue,
    CountOver(TotalRevenue) RevenueCount
  Where TotalRevenue > AverageRevenue
```

The compiler materializes aliases in dependency layers. `TotalRevenue` is
created in the grouped query, `AverageRevenue` and `RevenueCount` are created
in the same next layer because both depend only on `TotalRevenue`, and the
alias filter is applied in the following outer query.

Aliases at the same dependency depth must share a layer. The compiler must
generate the minimum number of derived-table wrappers; it must not wrap
`RevenueCount` around `AverageRevenue` merely because it appears later in the
projection.

An alias dependency cycle is invalid:

```text
Orders:
    TotalA + <1> TotalB,
    TotalB + <1> TotalA
```

Alias references in `Where`, `Order`, or another projection expression are
resolved only after the referenced projection layer has been materialized.
Predicates that use only source fields remain in the inner query so they can
still be applied before grouping or window calculations.

## 11. Expressions and functions

Arithmetic expressions can be used in projections and function arguments:

```text
OrderItems: ProductId,
    SUM(Quantity * UnitPrice) Revenue,
    SUM(Quantity * (UnitPrice - CostPrice)) GrossProfit
```

Parentheses preserve expression precedence:

```text
Orders: Id,
    ROUND(<100> * PaidAmount / NULLIF(TotalAmount, <0>), <2>) PaidPercent
```

Common functions include:

```text
COUNT(expression)
COUNT(DISTINCT expression)
COUNTIF(condition)
AVG(expression)
AVGIF(expression, condition)
SUM(expression)
MAX(expression)
MIN(expression)
ROUND(expression, digits)
NULLIF(value, otherValue)
COALESCE(value, fallback)
```

Use function names from metadata. Do not embed raw SQL function text or schema
qualification in a field name.

## 12. Readability rules

Prefer one semantic operation per context:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue
```

Use meaningful aliases:

```text
AverageOver(ProductSales.TotalRevenue) AverageProductRevenue
```

Keep clauses indented and put long projections on continuation lines.

When converting SQL to OQuery:

1. remove physical schema prefixes;
2. identify each derived table or CTE;
3. create a named context for each reusable derived result;
4. preserve each aggregation grain with the correct non-aggregate keys;
5. use explicit context joins and conditions;
6. use context-qualified aggregate filters for scalar comparisons;
7. use `*Over` functions when a scalar aggregate must appear as a column;
8. add deterministic ordering when result order matters.

## 13. SQL schema and generated output

OQuery uses logical names:

```text
Products
OrderItems
ProductSales
```

Generated SQL may contain physical names and schemas:

```sql
sa.Products
sa.OrderItems
```

The metadata provider owns this mapping. Do not add `sa.` to OQuery unless it
is explicitly registered as a logical entity name.
