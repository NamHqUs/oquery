# OQuery syntax guide

This document is the authoring reference for generating OQuery statements.
Use semantic entity and field names from metadata. Do not copy physical SQL
schema names into OQuery.

When a rule and an example disagree, the rule wins. Check the explicit rule
before relying on an example, and do not call a construct invalid without a
rule that says so.

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

Projection is the paragraph after an entity header when the query has no joins.
When it has joins, the projection starts with an explicit `Select` line after the
joins (see [Join placement](#join-placement)). It continues across lines
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

### Join placement

When a query has joins, write them directly under the header, followed by an
explicit `Select` line that starts the projection. `Where`, `Order`, `Skip`, and `Take` follow the projection, so
`Where` can filter on projection aliases.

```text
DiagnosisBurden is Visits:
  Left Join VisitLabs On Id = VisitLabs.VisitId
  Select Diagnose.Name Diagnosis,
    Count(distinct Id) VisitCount,
    (<100> * Sum(IfNull(VisitLabs.AbnormalCount, <0>))
        / NullIf(Sum(IfNull(VisitLabs.LabCount, <0>)), <0>)).Round(<2>) AbnormalLabPercent
  Where Status = <Completed>
  Order VisitCount DESC, AbnormalLabPercent DESC
  Take 10
```

A join is one line by default. A join that is complete at the end of its line
ends there, and the next line starts the projection with `Select`:

```text
Visits:
  Left Join VisitLabs On Id = VisitLabs.VisitId
  Select Id
```

A join that continues onto more lines must end with a top-level comma. The
comma ends the join and is not part of its condition. After any join, the
projection must start with an explicit `Select` line.

- A line that starts with `On` continues the join above it.
- An incomplete condition, such as one ending with `AND`, continues onto the next line.
- A multi-line join without the trailing comma is rejected before the projection.

```text
Visits:
  Left Join VisitLabs
    On Id = VisitLabs.VisitId AND
      VisitLabs.IsActive = <true>,
  Select Id
```

A query without joins keeps the header projection form, such as
`Employees: Id, Name`. A query with joins must have an explicit `Select` line. A join placed after a
header projection without `Select` is rejected.


Only one `Where` clause is allowed per query. Combine several conditions
explicitly with `AND` or `OR` inside that single clause; a second `Where`
line is a parse error:

```text
Where Status = <Completed> AND Quantity > <10> OR IsActive = <true>
```

Numeric constants may also be written without angle brackets. The parser
interprets both `2` and `<2>` as the numeric value `2`:

```text
Where Quantity > 10
Where Quantity > <10>
```

Paging clauses accept both forms as well:

```text
Take 10
Take <10>
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
Products:
  Inner Join ProductSales On Id = ProductSales.ProductId
  Select Id,
    Name,
    ProductSales.TotalRevenue TotalRevenue
```

`Join` without a type is an inner join:

```text
Products:
  Join ProductSales On Id = ProductSales.ProductId
  Select Id
```

Use an explicit condition when the context has one row per parent key:

```text
Customers:
  Left Join CustomerOrders On Id = CustomerOrders.CustomerId
  Select Id,
    Name,
    CustomerOrders.OrderCount OrderCount
```

A context relation without a condition declares the context as available to the
query. It must not be used to create a row-multiplying join unless the
generated SQL join condition is explicit.

### Joining one context more than once

The same context may be joined multiple times. Give each relation instance a
different alias and use that alias in the projection and join condition:

```text
User:
  Left Join "User Context" Manager1 On ManagerId = Id
  Left Join "User Context" Manager2 On ManagerId = Id
  Select Name, Manager1.Name, Manager2.Age

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
  Inner Join EmployeeTree aliasMembers
    On Id = aliasMembers.ManagerId
  Select FirstName ManagerName,
    Members.Count(Id) TotalMembers,
    aliasMembers.FirstName MemberName
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

`Any` and `NotAny` test whether a collection has matching rows. They are built-in
Boolean functions whose template and fragment rules are in
[FUNCTION-DEFINITIONS.md](./FUNCTION-DEFINITIONS.md#6-existence-operators).

```text
Products: Id, Name
  Where Users.Any(Status = <Active>)
    AND State = <CA>
```

`Users` is a collection relation of `Products`; the collection is the receiver.
The predicate is optional: `Users.Any()` tests only that a related row exists.
Use `Users.NotAny(...)` for the negative test. Programmatic query definitions use the
same function names.

`Any` and `NotAny` are also valid in a projection. Each one renders as a correlated
`EXISTS` or `NOT EXISTS` expression in the SELECT list, and the result is a Boolean column:

```text
Orders: Id
  Any(Items, Quantity >= <5>) HasBulk
  NotAny(Items) NoItems
```

The dotted form is equivalent: `Items.Any(Quantity >= <5>) HasBulk` and `Items.NotAny() NoItems`.

## 8. Aggregation and grouping

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

Fluent aggregate paths can aggregate any collection of rows. A collection may
come from:

1. a metadata collection relation;
2. a joined entity mapped to a table; or
3. a named context whose projection returns rows.

The receiver identifies the collection. Its alias is optional: when a join
does not specify an alias, use the joined entity or context name.

For a metadata collection relation:

```text
Employees:
    Id,
    Members.Count() MemberCount,
    Members.Sum(Salary) MemberSalaryTotal,
    Members.Avg(Salary) MemberAverageSalary
```

For a named context:

```text
Ref is User:
    Id,
    Name,
    ManagerId

User:
  Join Ref r On Id = r.ManagerId
  Select Id,
    Name Manager,
    r.Name Members,
    r.Count(r.Id) MemberCount
```

For a directly joined entity:

```text
User:
  Join User r On Id = r.ManagerId
  Select Id,
    Name Manager,
    r.Name Members,
    r.Count(r.Id) MemberCount
```

The alias may be omitted:

```text
User:
  Join User On Id = User.ManagerId
  Select Id,
    Name,
    User.Count(User.Id) MemberCount
```

`Count()` with no argument counts the related rows as `COUNT(1)`; no column is read.
`Sum`, `Avg`, `Min`, and `Max` require a numeric related field:

```text
Members.Count()
Members.Count(Id)
Members.CountIf(Name like <Bob>)
Members.CountIf(Id, Name like <Bob>)
Members.SumIf(Salary, Name like <Bob>)
Members.AvgIf(Salary, Name like <Bob>)
Members.MinIf(Salary, Name like <Bob>)
Members.MaxIf(Salary, Name like <Bob>)
Members.Sum(Salary)
```

For metadata collection relations, the SQL generator renders these parent
aggregates through a grouped relation source joined by the relationship key.
Multiple aggregates over the same collection reuse one grouped source.

The grouped relation source is an aggregation barrier. It is evaluated at the
parent relation-key grain before ordinary detail joins are applied. Therefore,
adding a collection detail projection must not change a collection aggregate:

```text
User:
    Id,
    Members.Count(Id) TotalMembers
```

and:

```text
User:
    Id,
    Members.Name MemberName,
    Members.Count(Id) TotalMembers
```

must produce the same `TotalMembers` for each `User.Id`. The second query may
return multiple rows per user because `Members.Name` is a detail projection,
but the count remains parent-scoped. SQL generation uses a parent-keyed
derived table for this barrier rather than calculating the aggregate in the
outer `GROUP BY`.

The same barrier applies to a joined entity or row-producing context when its
join has an equality condition:

```text
UserRows is User: Id, ManagerId

User:
  Join UserRows r On Id = r.ManagerId
  Select Id,
    r.Name Member,
    r.Count(Id) MemberCount
```

`MemberCount` is grouped by the joined source key `ManagerId` and then joined
back to `User.Id`. A `Member` detail projection therefore cannot change the
count. A context joined without a condition remains a query-wide aggregate
because no parent key is declared.

Collection aggregates are different from window aggregates. `Members.Count()`,
`r.Count(r.Id)`, and `Ref.Count(Id)` summarize rows from their respective
collections. A fluent suffix such as `.Avg()` then evaluates the result values
at the next query stage. By contrast,
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
fluent suffix. The source can be a relation aggregate, a joined
entity/context aggregate, or a projection alias:

```text
Products:
    Name,
    OrderItems.Sum(Quantity * UnitPrice) Revenue,
    OrderItems.Sum(Quantity * UnitPrice).Avg() AverageRevenue
```

For example, a context collection can be aggregated and then chained:

```text
OrderCategory is OrderItems:
    OrderId,
    Product.Category Category,
    SUM(Quantity * UnitPrice) NetRevenue

Report is OrderCategory:
  Join OrderCategory
  Select Category,
    OrderCategory.Sum(NetRevenue).Avg() AverageRevenue
```

The receiver must identify a row-producing collection. A scalar context value
is not a collection receiver; use the scalar field directly in an ordinary
aggregate instead:

```text
AverageRevenue is ProductSales:
    AVG(TotalRevenue) AverageTotalRevenue
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

A collection aggregate may also take an aggregate over a nested collection as
its argument. The inner aggregate is evaluated for each row of the outer
collection, and the outer aggregate then combines those values per root row:

```text
Users:
    Name,
    Members.Sum(Members.Count(Id)) MemberReportTotal
```

For each user, `Members.Count(Id)` counts the reports of each member, and
`Members.Sum(...)` adds those counts. A user with no members returns `0` for
`Count` and `NULL` for `Sum`, as with the single-level `Sum`. The engine chooses
the join and grouping steps; the query is still one expression.

Choose the root entity at the grain you want to report. A nested collection
aggregate reaches through relations from each root row, so the root decides
what one result row means. To report one row per patient, with totals over
their visits and each visit's lab results, use `Patients` as the root:

```text
HighRiskPatientCare is Patients:
    Id PatientId,
    FirstName,
    LastName,
    BirthDate.YearsTo(<2025-10-31>) Age,
    Visits.SumIf(Charge, Status = <Completed>).Round(<2>) TotalCharges,
    Visits.CountIf(Status = <Completed>) CompletedVisits,
    Visits.CountIf(Diagnose.IsChronic = <True>) ChronicVisits,
    Visits.Sum(LabResults.CountIf(IsAbnormal = <True>)) AbnormalResults
  Where AbnormalResults > <0>
  Order AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
  Take 10
```

Nested aggregates are where grain mistakes happen. Before writing one, follow
the checklist in [OQUERY-GENERATION-NOTES.md](./OQUERY-GENERATION-NOTES.md)
section 0 and state the business entity and the result row.

A root on `Visits` would give one row per visit. Aggregating patient totals
from that root would then need a second grain, so the query is harder to read
and easier to get wrong. The root grain also controls null handling: a patient
with no visits gets `NULL` for `Sum` and `0` for `Count`, as above.

## 9. Aggregate filters and scalar context aggregates

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

Products:
  Inner Join ProductSales On Id = ProductSales.ProductId
  Select Id,
    Name,
    ProductSales.TotalRevenue TotalRevenue
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

## 10. Aggregate-over window functions

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
Products:
  Inner Join ProductSales On Id = ProductSales.ProductId
  Select Id,
    Name,
    ProductSales.TotalRevenue TotalRevenue,
    AverageOver(ProductSales.TotalRevenue) OverallAverageRevenue,
    SumOver(ProductSales.TotalRevenue) OverallRevenue,
    CountOver(ProductSales.ProductId) ProductCount,
    MaxOver(ProductSales.TotalRevenue) MaximumRevenue,
    MinOver(ProductSales.TotalRevenue) MinimumRevenue
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

## 11. Projection alias dependencies

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

## 12. Expressions and functions

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

The full built-in function list, with arguments and per-dialect templates, is in
[FUNCTION-DEFINITIONS.md](./FUNCTION-DEFINITIONS.md). Common functions include:

```text
COUNT(expression)
Count(distinct expression)
COUNTIF(condition)
COUNTIF(expression, condition)
CountIf(distinct expression, condition)
AVG(expression)
AVGIF(expression, condition)
SUM(expression)
MAX(expression)
MIN(expression)
ROUND(expression, digits)
NULLIF(value, otherValue)
IfNull(value, fallback)
Concat(first, separator, last)
```

`COUNTIF` follows the same rules as `COUNT`:

- `CountIf(condition)` counts the rows that match the condition. It is rendered
  as `COUNT(CASE WHEN condition THEN 1 END)`.
- `CountIf(expression, condition)` counts the non-null `expression` values on
  the rows that match the condition, like `SumIf` and `AvgIf`.
- Prefix the expression with `distinct` to count unique expression values:
  `CountIf(distinct expression, condition)` renders as
  `COUNT(DISTINCT CASE WHEN condition THEN expression END)`.

Projection distinctness is written after the query header:

```text
Products: distinct Id, Name
```

It renders as `SELECT DISTINCT Id, Name`. Distinct applies to the complete
projected row, while aggregate distinctness applies only to the aggregate
expression.

Functions may also be called with fluent syntax. The expression before the dot
is passed as the first function argument:

```text
OrderItems: ROUND(SUM(Amount), <2>) RoundedAmount,
    SUM(Amount).ROUND(<2>) FluentRoundedAmount
```

A scalar field can also be the receiver. The field becomes the first argument,
and the field's data type must match the function's first argument type:

```text
Employees: Id,
    HireDate.DateSub(<2025-10-31>) TenureDays,
    HireDate.DateAdd(<30>) ReviewDate
```

`HireDate.DateSub(<2025-10-31>)` is `DateSub(HireDate, <2025-10-31>)`. Because
`HireDate` is `Datetime`, only functions whose first argument is `Datetime`
(for example `DateSub`, `DateAdd`, `MonthSub`, `YearAdd`, `Day`) are valid after
the dot. Using a function whose first argument type differs, such as
`Name.DateSub(...)` on a `String` field, is rejected during query translation.

A parenthesized expression can also be the receiver. The whole group becomes the
first argument, so arithmetic over aggregates can be rounded or formatted with
fluent syntax:

```text
Employees: Department.Name Department,
    (CountIf(Rating >= 4) * 100.0 / Count(Id)).Round(<2>) HighRatingPercent
```

`(expr).Round(<2>)` is `Round((expr), <2>)`. The group is evaluated like any other
projection expression, so aggregates inside it use the root context.

Use function names from metadata. Do not embed raw SQL function text or schema
qualification in a field name.

## 13. Readability rules

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

For SQL-to-OQuery conversion steps, see
[OQUERY-GENERATION-NOTES.md](./OQUERY-GENERATION-NOTES.md).

## 14. SQL schema and generated output

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
