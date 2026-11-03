# QueryDefinition semantic modeling guide

This guide records the important query-shape lessons from the sample reports.
The goal is not to reproduce SQL syntax. The goal is to preserve the same
business context and result using entities, relation paths, projections,
filters, aggregates, contexts, and controlled functions.

The YAML below is intentionally abbreviated. It shows the semantic
`QueryDefinition` shape, not a complete serialized request. The canonical
backend JSON projection shape is documented in
[`QUERY-MODEL-REFERENCE.md`](./QUERY-MODEL-REFERENCE.md).

Complete sample requests and their YAML counterparts are stored in
[`data-storage/queries`](./data-storage/queries).

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
  - State | Region
  - AVG(CustomerBestMonth.BestMonthRevenue) | AverageBestMonth
Contexts:
  - Name: CustomerMonth
    Query:
      Entity: Orders
      Projection:
        - UserId
        - YEAR(OrderDate) | SalesYear
        - MONTH(OrderDate) | SalesMonth
        - SUM(Items.Quantity * Items.UnitPrice - Items.DiscountAmount) | MonthlyRevenue
      Filter: Status = 'Completed'

  - Name: CustomerBestMonth
    Query:
      Entity: CustomerMonth
      Projection:
        - UserId
        - MAX(MonthlyRevenue) | BestMonthRevenue
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
  - Id | CustomerId
  - Concat(FirstName, ' ', LastName) | Customer
  - RecentOrders.Id | OrderId
  - RecentOrders.OrderDate | OrderDate
  - RecentOrders.OrderAmount | OrderAmount
Filter: RecentOrders.OrderRank <= 3
Contexts:
  - Name: RankedOrders
    Query:
      Entity: Orders
      Projection:
        - UserId
        - Id
        - OrderDate
        - SUM(Items.Quantity * Items.UnitPrice - Items.DiscountAmount) | OrderAmount
        - RowNumber(UserId, OrderDate) | OrderRank
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
  - Id | UserId
Filter: CompletedOrders.UserId IS NOT NULL
     OR ActivePolicies.UserId IS NOT NULL
Contexts:
  - Name: CompletedOrders
    Query:
      Entity: Orders
      Projection:
        - UserId
        - CountDistinct(Id) | MatchCount
      Filter: Status = 'Completed'
    Join: Users.Id = CompletedOrders.UserId
    JoinType: Left

  - Name: ActivePolicies
    Query:
      Entity: Policies
      Projection:
        - UserId
        - CountDistinct(Id) | MatchCount
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
    SELECT Id, ManagerId, 0 AS Level
    FROM Employees
    WHERE Id = @RootManagerId

    UNION ALL

    SELECT e.Id, e.ManagerId, t.Level + 1
    FROM Employees e
    JOIN EmployeeTree t ON e.ManagerId = t.Id
)
SELECT *
FROM EmployeeTree;
```

### Intended QueryDefinition shape

```yaml
Entity: Employees
Projection:
  - Id
  - ManagerId
  - 0 | Level
Filter: Id = @RootManagerId
Contexts:
  - Name: EmployeeTree
    Query:
      Entity: Employees
      Projection:
        - Id
        - ManagerId
        - Difference(EmployeeTree.Level, -1) | Level
      Join: Employees.ManagerId = EmployeeTree.Id
      Repeat: Until no rows
```

This sample requires repeated evaluation of the same relationship until a
fixed point is reached. The compiler emits a recursive CTE for this shape,
including the anchor query and the recursive query. A finite relation path
such as `Manager.DirectReports` remains supported as well.

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
  - Id | PolicyId
  - Concat(User.FirstName, ' ', User.LastName) | Member
  - Coalesce(RevenueSinceStart.PremiumRevenue, 0) | RevenueSinceSignup
Contexts:
  - Name: RevenueSinceStart
    Query:
      Entity: PremiumPayments
      Projection:
        - PolicyId
        - SUM(Amount) | PremiumRevenue
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
  - Id | EmployeeId
  - Salary
  - Rank(DepartmentId, Salary) | DepartmentRank
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
  - COUNT(Id) | Employees
Filter: COUNT(Id) >= 5
```

Aggregate predicates are routed to `HAVING` by the compiler.

### Filters across independent contexts

```yaml
Entity: Users
Projection:
  - Id | UserId
Filter: OrderSummary.OrderCount > 3
     OR PolicySummary.PolicyCount > 0
Contexts:
  - Name: OrderSummary
    Query:
      Entity: Orders
      Projection:
        - UserId
        - CountDistinct(Id) | OrderCount
    Join: Users.Id = OrderSummary.UserId
    JoinType: Left

  - Name: PolicySummary
    Query:
      Entity: Policies
      Projection:
        - UserId
        - CountDistinct(Id) | PolicyCount
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
  - CountIf(Status = 'Active') | ActiveCount
  - CountDistinct(Orders.Id) | OrderCount
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
