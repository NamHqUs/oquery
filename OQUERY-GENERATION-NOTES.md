# OQuery generation notes

This document holds the workflow and lessons for generating OQuery from SQL.
For the language rules, see [OQUERY-SYNTAX.md](./OQUERY-SYNTAX.md). For function
signatures, see [FUNCTION-DEFINITIONS.md](./FUNCTION-DEFINITIONS.md).

## 1. Converting SQL to OQuery

1. Remove physical schema prefixes.
2. Identify each derived table or CTE.
3. Create a named context for each reusable derived result.
4. Preserve each aggregation grain with the correct non-aggregate keys.
5. Use explicit context joins and conditions.
6. Use context-qualified aggregate filters for scalar comparisons.
7. Use `*Over` functions when a scalar aggregate must appear as a column.
8. Add deterministic ordering when result order matters.

## 2. Lessons learned

### Main query must be an entity header

The main query must start from an entity header such as `OrderItems:`. A
context whose source is another context, such as `SalesTrend is MonthlySales:`,
cannot be the main query. The engine rejects it with
`Entity [MonthlySales] does not exist in domain`. Declare contexts first and
join them into an entity-based main query.

### Window functions over aggregates are not yet verified

Window functions such as `Lag` or `WindowSum` over an aggregate alias in the
same context are not yet verified against the engine. Confirm the behavior
before relying on it.

### Verify inferred syntax

If a pattern is inferred from a general rule rather than shown in a documented
example, state the assumption before giving the query so it can be checked.
