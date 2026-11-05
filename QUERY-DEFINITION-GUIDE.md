# QueryDefinition semantic modeling guide

This guide records the important query-shape lessons from the sample reports.
The goal is not to reproduce SQL syntax. The goal is to preserve the same
business context and result using entities, relation paths, projections,
filters, aggregates, contexts, and controlled functions.

The legacy YAML examples below are intentionally abbreviated. They show the
semantic `QueryDefinition` shape, not the current authoring format. The
canonical backend JSON projection shape is documented in
[`QUERY-MODEL-REFERENCE.md`](./QUERY-MODEL-REFERENCE.md).

Complete sample requests and their serialized counterparts are stored in
[`data-storage/queries`](./data-storage/queries).

## OQuery text syntax

Saved query definitions use the compact `.oqr` format. A query header uses the
entity name as its default name:

```text
Users: Id, Name, BestMonth.Revenue
  Where Status = <Active>
  Order BestMonth.Revenue DESC
  Take 25
  Left Join BestMonth on Id = BestMonth.UserId
```

A reusable context puts its name before its source entity. If the names are
the same, the source is omitted:

```text
BestMonth is Orders: UserId, MAX(Amount) Revenue
  Where Status = <Completed>
```

The header grammar is `[context-name] is [entity]: projection`. `where`, `order`,
`skip`, `take`, and `join` are optional indented clauses. Join types are written
before `join`, for example `left join` or `inner join`; a bare `join` defaults
to `inner join`, matching SQL. A join condition is
explicit because it describes how the consuming query uses the context; the
context definition remains reusable by other queries. The parser resolves
contexts after reading all blocks, so a context can be referenced before its
definition.

## OQuery syntax reference

### Query headers

```text
Users: Id, Name
```

An entity-only query is the compact form of selecting every field exposed by
the metadata:

```text
Users
```

Equivalent SQL intent:

```sql
SELECT *
FROM Users;
```

The single name is both the query name and source entity. A reusable context
whose name differs from its source uses `is`:

```text
BestMonth is Orders: UserId, MAX(Amount) Revenue
```

The header grammar is:

```text
[ContextName] [is SourceEntity]: Projection
```

### Clauses

Clauses are indented under their query header:

```text
Users: Id, Name
  Where Status = <Active>
  Order Name ASC
  Skip 20
  Take 25
```

Keywords are case-insensitive. The canonical writer uses `Where`, `Order`,
`Skip`, `Take`, and join types such as `Left Join`.

### Projection aliases

Use the expression-to-name form consistently:

```text
Users: Id, Manager.Name ManagerName, Count(Members.Id) MemberCount
```

Separate the expression and result alias with whitespace:

```text
Users: Id, Manager.Name ManagerName, Count(Members.Id) MemberCount
```

The explicit `as` form is also accepted when an unambiguous alias marker is
preferred, especially for generated OQuery:

```text
Users: Id, Manager.Name as ManagerName, Count(Members.Id) as MemberCount
```

### Joins and aliases

Explicit joins belong to the query that consumes the context:

```text
Products: Id, Name,
    RevenueByMonth.Month Month,
    RevenueByMonth.Revenue Revenue
  Left Join RevenueByMonth On Id = RevenueByMonth.ProductId
  Order Revenue DESC
  Take 20

RevenueByMonth is InvoiceLines: ProductId,
    MonthKey(Invoice.IssueDate) Month,
    SUM(Quantity * UnitPrice) Revenue,
    CountDistinct(InvoiceId) Orders
  Where Invoice.Status = <Paid>
```

A reusable context can be joined more than once with a separate alias:

```text
User: Id, ref1.Name Manager1, newRef1.Name Manager2
  Left Join ref1 On ManagerId = ref1.Id
  Left Join ref1 newRef1 On ManagerId = newRef1.Id

ref1 is User: Id, Name
```

`ref1` is the shared context name. `newRef1` is only the alias for the second
join instance. A bare `Join` means `Inner Join`; use `Left Join`, `Right Join`,
or `Full Join` explicitly when optional rows are required.

### Multiline values

Projection and clause values can continue on indented lines:

```text
Visits: Diagnosis.Name Diagnosis,
    Diagnosis.Category DiagnosisCategory,
    CountDistinct(Id) VisitCount
  Order VisitCount DESC,
    Diagnosis.Name ASC
  Left Join VisitLabs On Id = VisitLabs.VisitId
```

Continuation lines are joined until the next recognized clause or query
header.

## Automatic query behavior

OQuery turns intent into relational operations using registered metadata.

```text
Users: Id, Name, Manager.Name
```

The relation path can generate the required join automatically.

```text
Users: Id, Name, Count(Members.Id) TotalMembers
```

Mixing regular fields and aggregates establishes the result grain and derives
the required `GROUP BY`.

```text
Users: Id, Name, Count(Members.Id) TotalMembers
  Where TotalMembers > 12
```

Aggregate predicates are routed to `HAVING`; row predicates remain `WHERE`.
This lets users express the business condition without manually moving the
predicate between SQL clauses.

## Reusable contexts and isolated composition

Contexts form a named dependency graph. A context owns the relationships it
declares, and its definition can be reused by multiple consumers:

```text
Products: Id, RevenueByMonth.Revenue Revenue
  Left Join RevenueByMonth On Id = RevenueByMonth.ProductId

RevenueByMonth is InvoiceLines: ProductId,
    SUM(Quantity * UnitPrice) Revenue
  Where Invoice.Status = <Paid>
```

The engine emits dependent CTEs in declaration order. A context can be tested
on its own by selecting only its block:

```text
ref1 is User: Id, Name
```

This supports an incremental workflow: test a context, test its dependent
context, then execute the root query.

## Application vocabulary

Registered typed functions extend the language with domain concepts:

```text
Users: Id,
    CountIf(Status = <Active>) ActiveMembers,
    AvgIf(Age, Status = <Active>) AverageActiveAge
```

Functions can define typed arguments, validation, editor completion, and
provider-specific SQL templates. This keeps business vocabulary such as
`CountIf`, `AgeOn`, `MonthKey`, and `RiskScore` out of duplicated raw SQL.

## Security and provider boundaries

OQuery is designed for controlled read queries:

1. Metadata defines the allowed entities, fields, relationships, and functions.
2. Authorization is checked against the query metadata before SQL is trusted.
3. Values become database parameters; user values are not concatenated into SQL.
4. The selected SQL dialect renders quoting, paging, functions, and provider
   differences.

OQuery is not a replacement for DDL, DML, stored procedures, or database
administration. Its scope is metadata-driven, parameterized read queries.

## Using the UI

1. Open **Data sources** and inspect entities, fields, and relationships.
2. Open **Query Builder** and start with an entity such as `Users`.
3. Switch to **OQuery** mode to edit the compact text directly.
4. Use **Check OQuery** to parse and normalize the definition.
5. Select a complete root or context block to execute only that query graph.
6. Use **Get SQL** to inspect the generated statement and parameters.
7. Save the query as a reusable report after the result is correct.

The editor supports Tab and Shift+Tab indentation, selected-row deletion with
Ctrl+X, undo with Ctrl+Z, and multiline projections and clauses.

Long projections may continue on indented lines. Continuation lines are joined
until the next recognized clause:

```text
Visits: Diagnosis.Name Diagnosis,
    Diagnosis.Category DiagnosisCategory,
    ROUND(<100> * SUM(Amount) / NULLIF(SUM(Total), <0>), <2>) Percent
  Where Status = <Completed>
  Take 10
```

Projection fields can use either a block sequence or YAML's inline sequence
syntax:

```yaml
Entity: Users
Projection: [Id, Name]
```

## Flat context graph

Contexts are stored once in the `Contexts` dictionary. The dictionary key is
the context name and is the identity used by `Relations`. A relation contains
only the referenced context name and join metadata; it does not embed another
query definition.

```yaml
Entity: Employees
Relations:
  - RefContext: C1
  - RefContext: C2

Contexts:
  C1:
    Entity: Departments
    Projection:
      - Id

  C2:
    Entity: Employees
    Relations:
      - RefContext: C1
        Alias: Department
        Condition: DepartmentId = C1.Id
        JoinType: Left
```

This shape allows multiple contexts to share `C1` without repeating its
definition. The parser still accepts the older nested `Contexts` and
`ExtraRelations` forms and normalizes them into this graph. Missing context
references and conflicting definitions are rejected during parsing.

The YAML writer uses the DTO names `Relations` and `RefContext`, never
`ExtraRelations` or `RefContextId`. Context names appear only as dictionary
keys, not additional `Id` or `Name` properties. Null `Alias`, `Condition`,
and `JoinType` properties are omitted. An omitted canonical YAML `JoinType`
round-trips as null; an explicitly supplied `Left` remains `Left`.
Legacy `ExtraRelations` input retains its historical default of `Left`.

## Core rules

1. Start with the result grain: one row per user, policy, department, order,
   or other business key.
2. Put each independent one-to-many aggregate in its own context.
3. A context must return one row per key used by its join condition unless the
   result intentionally needs detail rows.
4. Declare dependent contexts in dependency order. A later context may use an
   earlier context as its source.
5. Use relation paths for correlated values when the relationship is
   deterministic, for example `PaymentDate >= Policy.StartDate`.
6. Use registered, typed functions for provider-specific expressions such as
   `CountIf`, `AvgIf`, `Rank`, and `RowNumber`. Do not create Raw SQL entities
   or bypass the compiler.
7. Apply paging at the result grain. For grouped reports, aggregate in a
   context and page the outer result when necessary.
8. Add deterministic secondary ordering when the original SQL leaves ties
   unspecified and the test compares row order.

## 1. Nested aggregation through dependent contexts

### Original SQL

```sql
WITH customer_month AS
(
    SELECT
        CustomerId,
        YEAR(OrderDate) AS SalesYear,
        MONTH(OrderDate) AS SalesMonth,
        SUM(Amount) AS MonthlyRevenue
    FROM Orders
    GROUP BY CustomerId, YEAR(OrderDate), MONTH(OrderDate)
)
SELECT
    Region,
    AVG(MAX(MonthlyRevenue)) AS AverageBestMonth
FROM customer_month
GROUP BY Region;
```

### Semantic QueryDefinition shape

```yaml
Entity: Users
Projection:
  - State Region
  - AVG(CustomerBestMonth.BestMonthRevenue) AverageBestMonth
Contexts:
  - Name: CustomerMonth
    Query:
      Entity: Orders
      Projection:
        - UserId
        - YEAR(OrderDate) SalesYear
        - MONTH(OrderDate) SalesMonth
        - SUM(Items.Quantity * Items.UnitPrice - Items.DiscountAmount) MonthlyRevenue
      Filter: Status = 'Completed'

  - Name: CustomerBestMonth
    Query:
      Entity: CustomerMonth
      Projection:
        - UserId
        - MAX(MonthlyRevenue) BestMonthRevenue
    Join: Users.Id = CustomerBestMonth.UserId
```

`CustomerMonth` is declared before `CustomerBestMonth`. The second context
uses the first context as its source, so the two aggregation grains remain
separate.

Sample request:
[Sample-Nested customer monthly peak by region.json](./data-storage/queries/Sample-Nested%20customer%20monthly%20peak%20by%20region.json)

## 2. Per-parent detail paging/top-N

### Original SQL

```sql
SELECT
    c.Id,
    c.Name,
    o.Id,
    o.OrderDate,
    o.Amount
FROM Customers c
OUTER APPLY
(
    SELECT TOP (3)
        o.Id,
        o.OrderDate,
        o.Amount
    FROM Orders o
    WHERE o.CustomerId = c.Id
    ORDER BY o.OrderDate DESC
) o;
```

### Semantic QueryDefinition shape

```yaml
Entity: Users
Projection:
  - Id CustomerId
  - Concat(FirstName, ' ', LastName) Customer
  - RecentOrders.Id OrderId
  - RecentOrders.OrderDate OrderDate
  - RecentOrders.OrderAmount OrderAmount
Filter: RecentOrders.OrderRank <= 3
Contexts:
  - Name: RankedOrders
    Query:
      Entity: Orders
      Projection:
        - UserId
        - Id
        - OrderDate
        - SUM(Items.Quantity * Items.UnitPrice - Items.DiscountAmount) OrderAmount
        - RowNumber(UserId, OrderDate) OrderRank
      Filter: Status = 'Completed'

  - Name: RecentOrders
    Query:
      Entity: RankedOrders
      Projection:
        - UserId
        - Id
        - OrderDate
        - OrderAmount
        - OrderRank
    Join: Users.Id = RecentOrders.UserId
```

`RowNumber` is partitioned by the parent key, and the outer filter is applied
after the window value has been materialized in the dependent context. This is
per-parent top-N, not one global page of orders.

Sample request:
[Sample-Correlated top three orders per customer.json](./data-storage/queries/Sample-Correlated%20top%20three%20orders%20per%20customer.json)

## 3. UNION as a common-root existence filter

### Original SQL

```sql
SELECT UserId
FROM Orders
WHERE Status = 'Completed'

UNION

SELECT UserId
FROM Policies
WHERE Status = 'Active';
```

### Semantic QueryDefinition shape

```yaml
Entity: Users
Projection:
  - Id UserId
Filter: CompletedOrders.UserId IS NOT NULL
     OR ActivePolicies.UserId IS NOT NULL
Contexts:
  - Name: CompletedOrders
    Query:
      Entity: Orders
      Projection:
        - UserId
        - CountDistinct(Id) MatchCount
      Filter: Status = 'Completed'
    Join: Users.Id = CompletedOrders.UserId
    JoinType: Left

  - Name: ActivePolicies
    Query:
      Entity: Policies
      Projection:
        - UserId
        - CountDistinct(Id) MatchCount
      Filter: Status = 'Active'
    Join: Users.Id = ActivePolicies.UserId
    JoinType: Left
```

The common `Users` root turns the set union into an existence predicate.
`CountDistinct` makes each context one row per user, preventing duplicate
users after the joins.

Do not replace `UNION` with `CompletedOrders EXISTS ActivePolicies` unless
intersection semantics are intended. `EXISTS` in that form means both sets,
not either set.

Sample request:
[Sample-Union completed orders and active policies.json](./data-storage/queries/Sample-Union%20completed%20orders%20and%20active%20policies.json)

## 4. Recursive hierarchy

### Original SQL

```sql
WITH EmployeeTree AS
(
    SELECT Id, ManagerId
    FROM Employees
    WHERE ManagerId IS NULL

    UNION ALL

    SELECT e.Id, e.ManagerId
    FROM Employees e
    JOIN EmployeeTree t ON e.ManagerId = t.Id
)
SELECT
    manager.FirstName AS ManagerName,
    member.FirstName AS MemberName,
    COUNT(member.Id) OVER (
        PARTITION BY manager.Id
    ) AS TotalMembers
FROM EmployeeTree tree
JOIN Employees member ON member.Id = tree.Id
LEFT JOIN Employees manager ON manager.Id = tree.ManagerId
GROUP BY manager.FirstName, member.FirstName;
```

### Intended QueryDefinition shape

```yaml
Entity: Employees
Projection:
  - Id
  - ManagerId
  - 0 Level
Filter: Id = @RootManagerId
Contexts:
  - Name: EmployeeTree
    Query:
      Entity: Employees
      Projection:
        - Id
        - ManagerId
        - Difference(EmployeeTree.Level, -1) Level
      Join: Employees.ManagerId = EmployeeTree.Id
      Repeat: Until no rows
```

This sample requires repeated evaluation of the same relationship until a
fixed point is reached. The compiler emits a recursive CTE for this shape,
including the anchor query and the recursive query. A finite relation path
such as `Manager.DirectReports` remains supported as well.

Recursive shorthand keeps the CTE projection limited to the fields required
for traversal. Calculated fields and aggregates that reference the recursive
member are rendered by the query consuming the CTE, after recursion has
completed:

```text
EmployeeTree is Employees:
    FirstName ManagerName,
    member.FirstName MemberName,
    CountOver(member.Id, manager.Id) TotalMembers
  Inner Join EmployeeTree member On Id = member.ManagerId
```

The anchor and recursive members therefore retain the same physical traversal
columns, while `MemberName` and the windowed `TotalMembers` are calculated in
the outer query. This avoids placing an aggregate inside the recursive member,
preserves one row per member, and keeps the recursive CTE column shape valid.

Sample request:
[Sample-Recursive employee hierarchy.json](./data-storage/queries/Sample-Recursive%20employee%20hierarchy.json)

## 5. Correlated aggregate through a relation path

### Original SQL

```sql
SELECT
    c.Id,
    c.Name,
    (
        SELECT SUM(o.Amount)
        FROM Orders o
        WHERE o.CustomerId = c.Id
          AND o.OrderDate >= c.CustomerSince
    ) AS RevenueSinceSignup
FROM Customers c;
```

### Semantic QueryDefinition shape

NorthStar uses policy enrollment as the outer business record and paid
premium payments as the revenue rows.

```yaml
Entity: Policies
Projection:
  - Id PolicyId
  - Concat(User.FirstName, ' ', User.LastName) Member
  - Coalesce(RevenueSinceStart.PremiumRevenue, 0) RevenueSinceSignup
Contexts:
  - Name: RevenueSinceStart
    Query:
      Entity: PremiumPayments
      Projection:
        - PolicyId
        - SUM(Amount) PremiumRevenue
      Filter: Status = 'Paid'
            AND PaymentDate >= Policy.StartDate
    Join: Policies.Id = RevenueSinceStart.PolicyId
    JoinType: Left
```

The outer-row value is available through the normal relation path
`Policy.StartDate`, so a correlated subquery is unnecessary.

Sample request:
[Sample-Correlated revenue since policy start.json](./data-storage/queries/Sample-Correlated%20revenue%20since%20policy%20start.json)

## 6. Partitioned ranking

### Original SQL

```sql
SELECT
    DepartmentId,
    EmployeeId,
    Salary,
    RANK() OVER
    (
        PARTITION BY DepartmentId
        ORDER BY Salary DESC
    ) AS DepartmentRank
FROM Employees;
```

### Semantic QueryDefinition shape

```yaml
Entity: Employees
Projection:
  - DepartmentId
  - Id EmployeeId
  - Salary
  - Rank(DepartmentId, Salary) DepartmentRank
Sorts:
  - DepartmentId ASC
  - DepartmentRank ASC
  - EmployeeId ASC
```

`Rank` is a registered typed function with provider-specific templates. It is
not a Raw SQL field or compiler escape hatch.

Sample request:
[Sample-Department salary rank.json](./data-storage/queries/Sample-Department%20salary%20rank.json)

## 7. Complex filters and filter grain

Filters are boolean expression trees over fields, relation paths, expressions,
context fields, and aggregates. The filter model supports nested parentheses,
`AND`, `OR`, comparisons, `LIKE`, `BETWEEN`, `IN`, null checks, and
parameterized values.

### Dictionary<string, object?>-level filters

```sql
SELECT *
FROM Employees
WHERE
    (DepartmentId = 10 OR DepartmentId = 20)
    AND (EmploymentType = 'Full-time' OR Salary >= 100000);
```

```yaml
Entity: Employees
Filter: >
  (DepartmentId = 10 OR DepartmentId = 20)
  AND (EmploymentType = 'Full-time' OR Salary >= 100000)
```

### Aggregate filters

```sql
SELECT DepartmentId, COUNT(*) AS Employees
FROM Employees
GROUP BY DepartmentId
HAVING COUNT(*) >= 5;
```

```yaml
Entity: Employees
Projection:
  - DepartmentId
  - COUNT(Id) Employees
Filter: COUNT(Id) >= 5
```

Aggregate predicates are routed to `HAVING` by the compiler.

### Filters across independent contexts

```yaml
Entity: Users
Projection:
  - Id UserId
Filter: OrderSummary.OrderCount > 3
     OR PolicySummary.PolicyCount > 0
Contexts:
  - Name: OrderSummary
    Query:
      Entity: Orders
      Projection:
        - UserId
        - CountDistinct(Id) OrderCount
    Join: Users.Id = OrderSummary.UserId
    JoinType: Left

  - Name: PolicySummary
    Query:
      Entity: Policies
      Projection:
        - UserId
        - CountDistinct(Id) PolicyCount
    Join: Users.Id = PolicySummary.UserId
    JoinType: Left
```

Independent one-to-many branches should be aggregated in separate contexts
before applying the outer boolean filter. This prevents one branch from
multiplying rows from another branch.

### Mixed row and aggregate predicates

A boolean expression must be evaluated at one consistent result grain. Do not
mix a raw row predicate and a group aggregate directly:

```text
Status = 'Active' OR OrderCount > 3
```

Instead, convert the row predicate into an aggregate at the group grain:

```yaml
Projection:
  - CountIf(Status = 'Active') ActiveCount
  - CountDistinct(Orders.Id) OrderCount
Filter: ActiveCount > 0 OR OrderCount > 3
```

This makes both sides of the `OR` group-level predicates. Unary `NOT` is not a
general text-filter operator; use the supported negative operators such as
`<>`, `NOT LIKE`, `NOT IN`, and `IS NOT NULL`, or apply an equivalent
De Morgan transformation.

## Validation principle

For every sample, validate both meanings separately:

1. Confirm the original business semantics can be represented by the
   `QueryDefinition` structure.
2. Execute the generated dynamic SQL against real data.
3. Execute an independent reference statement.
4. Compare columns, row count, nulls, values, and ordering.

The independent SQL statement is allowed only as a test oracle. The saved
request and dynamic engine must use normal metadata, contexts, expressions,
and registered functions.

## General capability boundary

The important boundary is not SQL syntax. It is whether the query can be
transformed into a finite dependency graph of relational contexts:

```text
Entity
  -> relation paths
  -> dependent contexts
  -> projections, filters, aggregates, and windows
  -> outer result
```

Contexts may depend on earlier contexts, so a query can be represented as a
finite context DAG. This covers the tested transformations for CTEs, nested
aggregation, union-style logic, correlated aggregates, per-parent top-N, and
partitioned ranking even when the generated SQL has a different shape from
the original statement.

Therefore, for finite SQL `SELECT` queries, no non-recursive context-level
counterexample has been established by the samples in this guide. A missing
provider-specific function is a missing registered function definition, not
an inherent QueryDefinition context limitation.

Recursive or fixpoint evaluation is supported when the selected SQL dialect
supports recursive CTEs. The compiler emits the dialect-specific recursive CTE
form, rejects recursive queries on unsupported dialects, and validates that the
anchor and recursive projections have the same columns in the declared CTE
order; ordinary finite context dependencies continue to use regular CTEs.
