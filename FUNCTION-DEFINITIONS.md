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

Date parts in templates are fixed per function (for example `DateSub` is
`DATEDIFF(day, ...)`). Dialect-specific rendering is listed in
[Dialect overrides](#dialect-overrides).

## 1. Aggregate functions

| Name | Arguments | Returns | Aggregate | SQL Server template | Description |
|---|---|---|---|---|---|
| `Count` | `value: Number = 1` | Number | yes | `COUNT({1})` | Counts rows or non-null values. `Count()` counts rows. |
| `CountDistinct` | `expression: Number` | Number | yes | `COUNT(DISTINCT {1})` | Counts distinct non-null values in a related collection. |
| `Sum` | `expression: Number` | Number | yes | `SUM({1})` | Sum of numeric values. |
| `Avg` | `expression: Number` | Number | yes | `AVG({1})` | Average of numeric values. |
| `Max` | `expression: Number` | Number | yes | `MAX({1})` | Maximum value. |
| `Min` | `expression: Number` | Number | yes | `MIN({1})` | Minimum value. |
| `CountIf` | `expression: Number = 1`, `predicate: Boolean` | Number | yes | `COUNT(CASE WHEN {2} THEN {1} END)` | Counts rows matching the predicate, or non-null expression values on matching rows. Binds from end. |
| `CountDistinctIf` | `expression: Number = 1`, `predicate: Boolean` | Number | yes | `COUNT(DISTINCT CASE WHEN {2} THEN {1} END)` | Counts distinct non-null expression values on matching rows. Binds from end. |
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

## 3. Scalar functions

| Name | Arguments | Returns | SQL Server template | Description |
|---|---|---|---|---|
| `NullIf` | `value: Number`, `otherValue: Number` | Number | `NULLIF({1}, {2})` | Null when the two values are equal; otherwise the first value. |
| `IfNull` | `value: Number`, `fallback: Number` | Number | `COALESCE({1}, {2})` | First non-null numeric value. |
| `Difference` | `left: Number`, `right: Number` | Number | `({1} - {2})` | Difference of two numeric values. |
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
| `AgeOn` | `birthDate: Datetime`, `asOfDate: Datetime` | Number | `(DATEDIFF(year, {1}, {2}) - CASE WHEN DATEPART(month, {2}) * 100 + DATEPART(day, {2}) < DATEPART(month, {1}) * 100 + DATEPART(day, {1}) THEN 1 ELSE 0 END)` | Completed years between two dates. |
| `ElapsedYears` | `startDate: Datetime`, `endDate: Datetime` | Number | `(DATEDIFF(day, {1}, {2}) / 365.2425)` | Fractional years between two dates. |

`DateFormat` is also registered for SQLite only. See
[Dialect overrides](#dialect-overrides).

## 5. Dialect overrides

Every dialect uses the base set above. The table lists the functions each
dialect changes or adds. A function not listed uses the SQL Server template.

| Dialect (`Key`) | Adds | Overrides |
|---|---|---|
| `SqlServer` | `GetDate`, `Round` | none |
| `Sqlite` | `GetDate`, `Round`, `DateFormat` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `AgeOn`, `ElapsedYears` |
| `Postgres` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `AgeOn`, `ElapsedYears`, `Lag` |
| `Cockroach` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `AgeOn`, `ElapsedYears`, `Lag` |
| `Firebird` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `IfNull`, `MedicalLossRatio`, `Concat`, `MonthKey`, `AgeOn`, `ElapsedYears`, `Lag` |
| `Maria` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `AgeOn`, `ElapsedYears`, `Lag` |
| `Oracle` | `GetDate`, `Round` | `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `MonthKey`, `AgeOn` |
| `SapHana` | `GetDate`, `Round` | `Concat`, `Day`, `Month`, `Year`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `IfNull`, `AgeOn`, `ElapsedYears`, `Lag` |
| `Db2` | `Round` | `MedicalLossRatio`, `Concat`, `DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`, `MonthKey`, `AgeOn`, `ElapsedYears`, `Lag` |

Dialect caveats:
- `Db2` `Lag` renders `LAG({1}, 1)`, so the `offset` argument is ignored.
- `Sqlite` uses the base `Lag` and `WindowSum`, which need SQLite 3.25 or newer.
- `Sqlite` `DateAdd`, `MonthAdd`, and `YearAdd` use `days`, `months`, and `years`
  as unit names, not `day`, `month`, and `year`.
- `DateFormat` exists only for `Sqlite` (see [MANUAL.md](./MANUAL.md)).

## 6. Existence operators

`HasAny` and `HasNone` are Boolean condition operators. They are not entries
in the function provider. The parser reads them as existence condition nodes,
and SQL generation emits an `EXISTS` or `NOT EXISTS` subquery.

- The source is an unquoted context reference. It can be a collection relation
  on the current entity, a metadata entity, or a named query context.
- The condition is optional. When present, it is evaluated in the source scope.
- An explicit source alias may precede the operator. Use it when the condition
  also references the outer context.
- `CurrentContext` refers to the outer query context.
- Combine an existence condition with other Boolean expressions using
  parentheses.

```text
User: Name
  Where Members HasAny Name = <Bob>
```

```text
User: Name
  Where Members HasNone Name = <David>
```

```text
User: Id
  Where Members Member HasAny Member.Name = <Bob>
```

```text
Products: Id, Name
  Where (Users HasAny Status = <Active>)
    AND State = <CA>
```

```text
Products: Id, Name
  Where Users Customer HasNone Customer.Status = <Inactive>
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
