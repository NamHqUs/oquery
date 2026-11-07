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

An entity header without an explicit context name is the main execution entry
point. It is equivalent to naming the context after the source entity:

```text
OrderItems:
```

means:

```text
OrderItems is OrderItems:
```

Use the explicit `ContextName is SourceEntity` form only when the main entry
point needs a different context name:

```text
ProductSales is OrderItems:
    Product.Name,
    SUM(Quantity * UnitPrice) TotalRevenue
```

An entity-only query selects the metadata fields:

```text
Products
```

The select-all and source-context wildcard semantics are defined centrally in
[OQUERY-ENGINE.md](./OQUERY-ENGINE.md).

Projection aliases use either whitespace or `as`:

```text
Products: Id ProductId, Name as ProductName
```

Use aliases when an expression should have a stable result-column name:

```text
Products: SUM(OrderItems.Quantity) UnitsSold
```

Projection is the paragraph after an entity header. It continues across lines
until a query clause or a new context declaration begins:

```text
Products: Id, Category.Name CategoryName
    UnitPrice
```

Each projection item is separated from the next item by either a top-level
comma or a top-level line break. A line break therefore starts the next
projection item even when the previous item has no trailing comma. Line breaks
inside parentheses, brackets, or angle-bracket values remain part of the
current expression.

The projection paragraph ends when a clause keyword begins:

```text
Products: Id, Category.Name CategoryName
  Where UnitPrice > <10>
```

After a field or relation path is complete, whitespace may introduce an
alias. The editor should not suggest another field until a comma begins the
next projection item.

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
- `Join` (inner join by default)
- `Inner Join`
- `Left Join`
- `Right Join`
- `Full Join`

Keywords are case-insensitive. Use the canonical capitalization shown above
for readable saved queries.

Values must use angle brackets:

```text
Where Status = <Completed>
Where Quantity > <10>
Where IsActive = <true>
```

If a numeric value is written without angle brackets, the parser reports the
required form, for example: `Numeric constant [2] must be enclosed in angle
brackets: <2>.`

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

The source entity name is also the default main execution context when no
explicit context name is provided. Therefore, `OrderItems:` is the shorthand
main entry point for `OrderItems is OrderItems:`. Use a different context name
only when the query intentionally overrides the entry-point name.

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

`Join` without a type is an inner join:

```text
Products: Id
  Join ProductSales On Id = ProductSales.ProductId
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

### Joining one context more than once

The same context may be joined multiple times. Give each relation instance a
different alias and use that alias in the projection and join condition:

```text
User: Name, Manager1.Name, Manager2.Age
  Left Join "User Context" Manager1 On ManagerId = Id
  Left Join "User Context" Manager2 On ManagerId = Id

"User Context" is User: Id, Name, Age
```

`Manager1` and `Manager2` are separate relation instances of the same
`User Context`. This supports self-joins and repeated context usage. Do not
report repeated context aliases as unsupported.

### Recursive context self-joins

A context that joins itself is compiled as a recursive query when the selected
SQL dialect supports recursive CTEs:

```text
EmployeeTree is Employees:
    FirstName ManagerName,
    Members.Count(Id) TotalMembers,
    aliasMembers.FirstName MemberName
  Inner Join EmployeeTree aliasMembers
    On Id = aliasMembers.ManagerId
```

The projection can combine metadata relationship expressions with fields from
the recursive context:

- `Members.Count(Id)` is a collection aggregate over the `Members` metadata
  relationship of the current employee;
- `aliasMembers.FirstName` is a field from the recursive context join;
- `COUNT(aliasMembers.Id)` counts rows from the recursive alias and is
  different from `Members.Count(Id)`.

Use an explicit join alias when projecting fields from the recursive context.
The alias identifies the recursive member scope and avoids confusing it with a
metadata relationship.

## 7. Collection existence conditions

`HasAny` and `HasNone` are Boolean condition operators. Their source is an
unquoted context reference that may resolve to a collection relation on the
current entity, a metadata entity, or a named query context:

```text
Products: Id, Name
  Where Users HasAny
```

The relationship is resolved from metadata when `Users` is a collection
relation. An optional condition is scoped to the source context:

```text
Products: Id, Name
  Where Users Customer HasAny Customer.Status = <Active>
```

The alias is optional:

```text
Products: Id, Name
  Where Users HasAny Status = <Active>
```

Use parentheses when a collection existence condition is combined with an
outer Boolean expression:

```text
Products: Id, Name
  Where (Users HasAny Status = <Active>)
    AND State = <CA>
```

`CurrentContext` refers to the outer query context. `HasNone` uses the same
source and condition rules:

```text
Products: Id, Name
  Where Users HasNone Customer.Status = <Inactive>
```

Existence conditions are represented in the query model as condition nodes.
The context token is stored as a constant value and resolved semantically;
it is not a SQL string parameter or a raw SQL field. SQL generation emits the
dialect-specific `EXISTS` or `NOT EXISTS` subquery.

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

The standard grouping aggregates are:

```text
COUNT(expression)
SUM(expression)
AVG(expression)
MAX(expression)
MIN(expression)
```

Collection relations also support fluent-style aggregate paths:

```text
Employees:
    Id,
    Members.Count() MemberCount,
    Members.Sum(Salary) MemberSalaryTotal,
    Members.Average(Salary) MemberAverageSalary
```

`Count()` uses the related entity key when no argument is supplied.
`Sum`, `Average`, `Min`, and `Max` require a numeric related field:

```text
Members.Count()
Members.Count(Id)
Members.Sum(Salary)
```

For metadata collection relations, the SQL generator renders these parent
aggregates through a grouped relation source joined by the relationship key.
Multiple aggregates over the same collection reuse one grouped source.

Collection aggregates are different from window aggregates. `Members.Count()`
summarizes related rows for the current parent, while
`CountOver(Member.Id, ManagerId)` calculates a window over the current result
rows and preserves their detail grain.

An aggregate-only context returns one aggregate row:

```text
AverageRevenue is ProductSales:
    AVG(TotalRevenue) AverageTotalRevenue
```

Do not use an ordinary aggregate in a detail projection when the desired
behavior is a scalar value repeated for each row. Use an explicit `*Over`
function or a fluent aggregate suffix instead.

### Fluent aggregate pipelines

Collection aggregates may be followed by one result-level aggregate using a
fluent suffix:

```text
Products:
    Name,
    OrderItems.Sum(Quantity * UnitPrice) Revenue,
    OrderItems.Sum(Quantity * UnitPrice).Avg() AverageRevenue
```

The collection aggregate is evaluated first, producing one `Revenue` value per
product. The suffix aggregate then evaluates those result values across the
current result rows and preserves the product rows. The compiler may render
this as an aggregate-over window expression, but the fluent syntax does not
expose that SQL implementation detail.

Supported result-level suffixes are:

```text
.Count()
.Sum()
.Avg()
.Max()
.Min()
```

Additional result-level aggregates may be chained. Each suffix is evaluated
in a new query stage, so a pipeline such as
`OrderItems.Sum(Quantity * UnitPrice).Avg().Sum()` remains valid without
nested window functions in the generated SQL.

## 8. Aggregate filters and scalar context aggregates

Aggregate predicates on the current query are routed to `HAVING`:

```text
Products: Id,
    Name,
    COUNT(OrderItems.Id) OrderCount
  Where COUNT(OrderItems.Id) > <5>
```

An aggregate over a named context in a filter is a scalar context aggregate:

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

This is distinct from a collection relation aggregate. Collection expressions
such as `Members.Count()` and `Members.Sum(Salary)` use the relation's
metadata keys and are rendered through a grouped relation source joined to the
parent. The scalar context form above is used when comparing a named context
aggregate with another aggregate and remains a scalar context operation.

Do not generate an ordinary `AVG(...)`, `SUM(...)`, or `MAX(...)` in a detail
projection when a scalar context aggregate is required. Use an aggregate-over
window function as described next when the value must be calculated over the
current detail rows.

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

All projection aliases declared in the same context are available to every
projection expression and predicate in that context, regardless of their
position. This lets a report place a calculated total in the final result
column while an earlier expression uses that total:

```text
OrderItems:
    ProductId,
    AverageOver(TotalRevenue) AverageRevenue,
    SUM(Quantity * UnitPrice) TotalRevenue,
    CountOver(TotalRevenue) RevenueCount
  Where TotalRevenue > AverageRevenue
```

The result keeps the requested projection order. A calculated total can
therefore remain the final report column even when earlier expressions use it.

An alias dependency cycle is invalid and should be rewritten as separate
contexts:

```text
Orders:
    TotalA + <1> TotalB,
    TotalB + <1> TotalA
```

Aliases can also be used in `Where` and `Order` expressions in the same
context.

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
