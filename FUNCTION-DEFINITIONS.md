# Built-in function definitions

This is the reference for every function the OQuery engine registers by
default. Use it when generating OQuery text or when checking whether a
function is available for a given SQL dialect.

Source of truth:
[SqlFunctionDefinitionBuiltinBase.cs](./src/OQuery/Contracts/SqlDialects/SqlFunctionDefinitionBuiltinBase.cs)
(base set, 38 functions) and the per-dialect
`*FunctionDefinition*.cs` / `*FunctionDefinitionBuiltin.cs` files under
[SqlDialects](./src/OQuery/Contracts/SqlDialects). This document was written
from those files. If the code changes, update this document.

## How to read this document

- **Name** is the function name used in OQuery text. It can differ from the
  C# method name, for example the C# `Average()` method registers as `Avg`.
- **Arguments** are listed as `name: DataType`. A default value is shown as
  `= default`. Arguments without a default are required.
- **Returns** is the logical `DataType` (`Number`, `String`, `Datetime`, or
  `Boolean`).
- **SQL Server template** uses `{1}`, `{2}`, ... for argument positions.
  Query values stay parameterized.
- **Aggregate** means the function collapses rows and drives `GROUP BY`.
  **Window** means it keeps the detail rows.
- **Binding from end** means arguments are matched from the end, so optional
  leading arguments can be omitted (see
  [FUNCTION-ARGUMENTS.md](./FUNCTION-ARGUMENTS.md)).
- `FunctionApplyFor` is a flags value identifying compatible inputs:
  `DataSet = 1`, `Text = 2`, `Number = 4`, `DateTime = 8`, and
  `Boolean = 16`. A template that uses `@Context` must include `DataSet`.
- Context-aware templates use two compiler-provided fragments:
  - `@Context` is the inner source of a related collection, with its alias
    (for example `"OrderItems" t1`) and any joins needed by the predicate.
  - `@JoinedConditions` is the correlation predicate built from relation
    metadata (for example `t1."ProductId" = t0."Id"`), with multiple keys
    joined by `AND`.
  An omitted fragment removes its connective: an absent predicate renders
  `WHERE @JoinedConditions`, an uncorrelated context renders `WHERE {1}`, and
  when both are absent the `WHERE` is dropped. `GetSqlTemplate(dialect, context,
  joinedConditions)` substitutes the two fragments into a template directly.

Date parts in templates are fixed per function (for example `DateSub` is
`DATEDIFF(day, ...)`). Dialect-specific rendering is listed in
[Dialect overrides](#dialect-overrides).

## 1. Aggregate functions

| Name | Arguments | Returns | Aggregate | SQL Server template | Description |
|---|---|---|---|---|---|
| `Count` | `value: Number = 1` | Number | yes | `COUNT({1})` | Counts rows or non-null values. `Count()` counts rows. |
| `Sum` | `expression: Number` | Number | yes | `SUM({1})` | Sum of numeric values. |
| `Avg` | `expression: Number` | Number | yes | `AVG({1})` | Average of numeric values. |
| `Max` | `expression: Number` | Number | yes | `MAX({1})` | Maximum value. |
| `Min` | `expression: Number` | Number | yes | `MIN({1})` | Minimum value. |
| `CountIf` | `expression: Number = 1`, `predicate: Boolean` | Number | yes | `COUNT(CASE WHEN {2} THEN {1} END)` | Counts rows matching the predicate, or non-null expression values on matching rows. Binds from end. |
| `SumIf` | `expression: Number`, `predicate: Boolean` | Number | yes | `SUM(CASE WHEN {2} THEN {1} END)` | Sums values on matching rows. |
| `AvgIf` | `expression: Number`, `predicate: Boolean` | Number | yes | `AVG(CASE WHEN {2} THEN {1} END)` | Averages values on matching rows. |
| `MinIf` | `expression: Number`, `predicate: Boolean` | Number | yes | `MIN(CASE WHEN {2} THEN {1} END)` | Minimum value on matching rows. |
| `MaxIf` | `expression: Number`, `predicate: Boolean` | Number | yes | `MAX(CASE WHEN {2} THEN {1} END)` | Maximum value on matching rows. |
| `MedicalLossRatio` | `claimsPaid: Number`, `premiumRevenue: Number` | Number | yes | `ROUND(100.0 * COALESCE(MAX({1}), 0) / NULLIF(MAX({2}), 0), 2)` | Paid claims as a percentage of premium revenue. |

## 2. Window functions

Window functions return a value per detail row and do not collapse rows.

| Name | Arguments | Returns | SQL Server template | Description |
|---|---|---|---|---|
| `CountOver` | `expression: Number`, `partitionBy: Number = *` | Number | `COUNT({1}) OVER (PARTITION BY {2})` | Count within a partition. |
| `SumOver` | `expression: Number`, `partitionBy: Number = *` | Number | `SUM({1}) OVER (PARTITION BY {2})` | Sum within a partition. |
| `AverageOver` | `expression: Number`, `partitionBy: Number = *` | Number | `AVG({1}) OVER (PARTITION BY {2})` | Average within a partition. |
| `MaxOver` | `expression: Number`, `partitionBy: Number = *` | Number | `MAX({1}) OVER (PARTITION BY {2})` | Maximum within a partition. |
| `MinOver` | `expression: Number`, `partitionBy: Number = *` | Number | `MIN({1}) OVER (PARTITION BY {2})` | Minimum within a partition. |
| `Lag` | `expression: Number`, `offset: Number`, `orderBy: String` | Number | `LAG({1}, {2}) OVER (ORDER BY {3} ASC)` | Value from the previous row in ascending order. |
| `WindowSum` | `expression: Number`, `orderBy: String` | Number | `SUM({1}) OVER (ORDER BY {2} ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)` | Running total in ascending order. |
| `Rank` | `partitionBy: Number`, `orderBy: Number` | Number | `RANK() OVER (PARTITION BY {1} ORDER BY {2} DESC)` | Rank within each partition, descending. |
| `RowNumber` | `partitionBy: Number`, `orderBy: Datetime` | Number | `ROW_NUMBER() OVER (PARTITION BY {1} ORDER BY {2} DESC)` | Row number within each partition, descending. |

`Rank` and `RowNumber` are registered with the aggregate flag even though
they are window functions. Treat them as window functions when writing OQuery.

Aggregate distinctness is written inside the aggregate call rather than as a
separate function name:

```text
Count(distinct expression)
CountIf(distinct expression, predicate)
```

For example, `CountIf(distinct Quantity * UnitPrice, Status = <Completed>)`
renders as `COUNT(DISTINCT CASE WHEN Status = ... THEN Quantity * UnitPrice END)`.

## 3. Scalar functions

| Name | Arguments | Returns | SQL Server template | Description |
|---|---|---|---|---|
| `NullIf` | `value: Number`, `otherValue: Number` | Number | `NULLIF({1}, {2})` | Null when the two values are equal; otherwise the first value. |
| `IfNull` | `value: Number`, `fallback: Number` | Number | `COALESCE({1}, {2})` | First non-null numeric value. |
| `Concat` | `first: String`, `separator: String`, `last: String` | String | `CONCAT({1}, {2}, {3})` | Concatenates three text values. |
| `Round` | `value: Number`, `digits: Number` | Number | `ROUND({1}, {2})` | Rounds to the given decimal places. SQL Server, and other dialects, append this. |
| `GetDate` | none | Datetime | `GETDATE()` | Current date and time. |

## 4. Date functions

| Name | Arguments | Returns | SQL Server template | Description |
|---|---|---|---|---|
| `Day` | `extractDate: Datetime` | Number | `DAY({1})` | Day part of a date. |
| `Month` | `extractDate: Datetime` | Number | `MONTH({1})` | Month part of a date. |
| `Year` | `extractDate: Datetime` | Number | `YEAR({1})` | Year part of a date. |
| `MonthKey` | `value: Datetime` | String | `CONVERT(char(7), {1}, 126)` | Year-month key, `YYYY-MM`. |
| `DateSub` | `startDate: Datetime`, `endDate: Datetime` | Number | `DATEDIFF(day, {1}, {2})` | Elapsed whole days. |
| `MonthSub` | `startDate: Datetime`, `endDate: Datetime` | Number | `DATEDIFF(month, {1}, {2})` | Calendar-month boundaries between dates. |
| `YearSub` | `startDate: Datetime`, `endDate: Datetime` | Number | `DATEDIFF(year, {1}, {2})` | Calendar-year boundaries between dates. |
| `DateAdd` | `value: Datetime`, `amount: Number` | Datetime | `DATEADD(day, {2}, {1})` | Adds days to a date. |
| `MonthAdd` | `value: Datetime`, `amount: Number` | Datetime | `DATEADD(month, {2}, {1})` | Adds months to a date. |
| `YearAdd` | `value: Datetime`, `amount: Number` | Datetime | `DATEADD(year, {2}, {1})` | Adds years to a date. |
| `ElapsedYears` | `startDate: Datetime`, `endDate: Datetime` | Number | `(DATEDIFF(day, {1}, {2}) / 365.2425)` | Fractional years between two dates. |
| `YearsTo` | `startDate: Datetime`, `endDate: Datetime` | Number | `(DATEDIFF(year, {1}, {2}) - CASE WHEN DATEPART(month, {2}) * 100 + DATEPART(day, {2}) < DATEPART(month, {1}) * 100 + DATEPART(day, {1}) THEN 1 ELSE 0 END)` | Completed years between startDate and endDate. Written as `HireDate.YearsTo(<2025-10-31>)`. |
| `MonthsTo` | `startDate: Datetime`, `endDate: Datetime` | Number | `(MonthSub({1}, {2}) - CASE WHEN Day({2}) < Day({1}) THEN 1 ELSE 0 END)` | Completed months between startDate and endDate. Written as `HireDate.MonthsTo(<2025-10-31>)`. |
| `DaysTo` | `startDate: Datetime`, `endDate: Datetime` | Number | `DateSub({1}, {2})` | Completed days between startDate and endDate. Written as `HireDate.DaysTo(<2025-10-31>)`. |
| `YearsToNow` | `startDate: Datetime` | Number | `YearsTo({1}, GetDate())` | Completed years from startDate until now. Written as `HireDate.YearsToNow()`. |
| `MonthsToNow` | `startDate: Datetime` | Number | `MonthsTo({1}, GetDate())` | Completed months from startDate until now. Written as `HireDate.MonthsToNow()`. |
| `DaysToNow` | `startDate: Datetime` | Number | `DaysTo({1}, GetDate())` | Completed days from startDate until now. Written as `HireDate.DaysToNow()`. |

`YearsTo`, `MonthsTo`, and `DaysTo` reuse each dialect's `MonthSub`, `Day`, and `DateSub` templates, and the `*ToNow` variants fill in the current time as the end date. The current-time expression is `GETDATE()` on SQL Server, `datetime('now', 'localtime')` on SQLite, `CURRENT TIMESTAMP` on Db2, and `CURRENT_TIMESTAMP` on the other dialects. `YearsTo` is overridden per dialect; `MonthsTo` and `DaysTo` are built from the base templates.

`DateFormat` is also registered for SQLite only. See
[Dialect overrides](#dialect-overrides).

## 5. Dialect overrides

Every dialect uses the base set above. The table lists the functions each
dialect changes or adds. A function not listed uses the SQL Server template.

| Dialect (`Key`) | Adds | Overrides |
|---|---|---|
| `SqlServer` | `GetDate`, `Round` | none |
| `Sqlite` | `GetDate`, `Round`, `DateFormat` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `ElapsedYears` |
| `Postgres` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `ElapsedYears`, `Lag` |
| `Cockroach` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `ElapsedYears`, `Lag` |
| `Firebird` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `IfNull`, `MedicalLossRatio`, `Concat`, `MonthKey`, `ElapsedYears`, `Lag` |
| `Maria` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `ElapsedYears`, `Lag` |
| `Oracle` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `MonthKey` |
| `SapHana` | `GetDate`, `Round` | `Concat`, `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `IfNull`, `ElapsedYears`, `Lag` |
| `Db2` | `Round` | `MedicalLossRatio`, `Concat`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `ElapsedYears`, `Lag` |

Dialect caveats:
- `Db2` `Lag` renders `LAG({1}, 1)`, so the `offset` argument is ignored.
- `Sqlite` uses the base `Lag` and `WindowSum`, which need SQLite 3.25 or newer.
- `Sqlite` `DateAdd`, `MonthAdd`, and `YearAdd` use `days`, `months`, and `years`
  as unit names, not `day`, `month`, and `year`.
- `DateFormat` exists only for `Sqlite` (see [MANUAL.md](./MANUAL.md)).

## 6. Existence operators

`Any` and `NotAny` are built-in Boolean functions registered with
`FunctionApplyFor.DataSet`. The compiler resolves the collection path and
renders the function's template; SQL text for `EXISTS` and `NOT EXISTS` comes
only from these definitions. The fluent `Any`/`NotAny` query methods normalize
to these functions.

| Name | Arguments | Returns | Template (all dialects) |
|---|---|---|---|
| `Any` | `predicate: Boolean` (omittable) | Boolean | `EXISTS (SELECT 1 FROM @Context WHERE {1} AND @JoinedConditions)` |
| `NotAny` | `predicate: Boolean` (omittable) | Boolean | `NOT EXISTS (SELECT 1 FROM @Context WHERE {1} AND @JoinedConditions)` |

- `{1}` is the predicate, parsed in the inner source scope. It is optional, so
  `Any(Members)` is valid.
- `@Context` is the inner source, for example `"User" t1`.
- `@JoinedConditions` is the correlation from the relation keys, for example
  `(t1."ManagerId" = t0."Id")`.
- With both fragments, `Any(Members, Name = <Bob>)` renders
  `EXISTS (SELECT 1 FROM "User" t1 WHERE (t1."Name" = @p0) AND (t1."ManagerId" = t0."Id"))`.
  Without a predicate, the `WHERE` holds only the correlation. Without
  correlation, it holds only the predicate. With neither, no `WHERE` is emitted.
- The source is an unquoted context reference: a collection relation on the
  current entity, or a dotted relation path such as `Members.Member`.
- The same functions work in a projection (SELECT list), where each call yields a
  Boolean column. `Orders: Id` with `Any(Items, Quantity >= <5>) HasBulk` renders
  `EXISTS (SELECT 1 FROM "OrderItems" t1 WHERE (t1."Quantity" >= @p0) AND (t1."OrderId" = t0."Id")) "HasBulk"`.
  The dotted form `Items.Any(...)` and `Items.NotAny()` is equivalent and is rewritten
  to the function form before compilation.
- Combine an existence condition with other Boolean expressions using
  parentheses.

```text
User: Name
  Where Any(Members, Name = <Bob>)
```

```text
User: Name
  Where NotAny(Members)
```

```text
User: Name
  Where NotAny(Members, Name = <David>)
```

```text
Products: Id, Name
  Where Any(Users, Status = <Active>)
    AND State = <CA>
```

```text
Products: Id, Name
  Where NotAny(Users, Status = <Inactive>)
```

## 7. Not function definitions

These are engine features, not entries in the function provider. They are
not listed above:
- Collection aggregates such as `Members.Sum(Salary)` and `Items.SumIf(...)`
  are resolved through metadata relations.
- Fluent suffix aggregates such as `.Avg()` and `.Sum()` on collection results.
- Arithmetic operators `+ - * / %`.
- Parentheses and fluent calls on expressions, such as `(expr).Round(<2>)`.

See [OQUERY-SYNTAX.md](./OQUERY-SYNTAX.md) for how these are written.
