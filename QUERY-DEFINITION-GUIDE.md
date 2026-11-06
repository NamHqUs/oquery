# QueryDefinition modeling guide

This guide explains how to model a business query with the current
`QueryDefinition` model. It focuses on query shape, result grain, context
composition, and execution behavior.

For authoring OQuery text, use [OQUERY-SYNTAX.md](./OQUERY-SYNTAX.md).
For the serialized JSON contract, use
[QUERY-MODEL-REFERENCE.md](./QUERY-MODEL-REFERENCE.md). For compiler
invariants and source-of-truth responsibilities, use
[OQUERY-ENGINE.md](./OQUERY-ENGINE.md).

## 1. Start with the result grain

Define what one output row represents before choosing fields or functions:

- one entity;
- one parent and a calculated child total;
- one row per period;
- one row per context key;
- one row per hierarchy member.

Every non-aggregate projection field defines or participates in the result
grain. Keep independent one-to-many calculations in separate contexts when
combining them would multiply rows.

## 2. Choose the root query

The root query is the main execution entry point. An unqualified OQuery
header uses its source entity as the context name. An explicit context name
overrides that name.

The root owns the final result columns, root filter, ordering, and paging.
Contexts are supporting query blocks that the root consumes.

## 3. Use a context for a reusable relation

A context is appropriate when a calculation:

- has a different result grain from the root;
- must be reused by more than one consumer;
- must be aggregated before another calculation;
- needs independent filtering or paging;
- represents a recursive or derived result.

Keep the context output narrow. Include the key required by its consumer and
only the fields needed by later projections or join conditions.

## 4. Story: aggregate at a stable grain

Use one context when a child collection must become one row per parent key:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue
```

`ProductId` defines the context grain. `TotalRevenue` is calculated within
that grain. A consumer can join this context without creating one row per
order item.

## 5. Story: dependent aggregation

Use dependent contexts when one aggregate must be completed before another
aggregate begins:

```text
ProductSales is OrderItems: ProductId,
    SUM(Quantity * UnitPrice) TotalRevenue

ProductMaximum is ProductSales:
    MAX(TotalRevenue) MaximumRevenue
```

Declare the source context before the context that consumes it. The compiler
creates the required derived layers and preserves the dependency boundary.

## 6. Story: consume a context from the root

The consuming query declares the relationship and its join condition:

```text
Products: Id,
    ProductSales.TotalRevenue TotalRevenue
  Left Join ProductSales On Id = ProductSales.ProductId
```

The context defines the calculated value; the root decides how that value is
matched to its own rows. Do not hide a row-multiplying relationship behind a
context that is expected to return one row per parent.

## 7. Story: aliases are a context-wide scope

All projection aliases in one context are available regardless of their
position in the requested output:

```text
OrderItems:
    AverageOver(TotalRevenue) AverageRevenue,
    SUM(Quantity * UnitPrice) TotalRevenue
```

The requested result order is preserved, while the engine resolves and
materializes the dependency before compiling `AverageRevenue`. See
[OQUERY-ENGINE.md](./OQUERY-ENGINE.md) for the compiler invariant.

Do not create cycles between aliases. Split cyclic calculations into separate
contexts with an explicit dependency direction.

## 8. Aggregates and windows

Use a grouping aggregate when the result should collapse to a grouping grain.
Use `CountOver`, `SumOver`, `AverageOver`, `MaxOver`, or `MinOver` when the
result must retain one row per input record and repeat a calculated value.

Aggregate filters on the current grouped query are part of the aggregate
stage. A scalar aggregate over a context-qualified field is a separate
context-level calculation, not a raw SQL fragment.

## 9. Filtering, ordering, and paging

- Filter source rows before grouping whenever the predicate uses only source
  fields.
- Filter aliases only after their projection layer exists.
- Order by a logical field or a materialized alias.
- Apply paging at the result grain.
- Add deterministic secondary ordering when ties matter.

Do not add `GroupBy` or `Having` properties to `QueryDefinition`; grouping is
derived from the projection and filter expressions.

## 10. Recursive contexts

Use one explicitly named context for a recursive hierarchy. The context must
identify its recursive relationship through the supported join structure, and
the selected SQL dialect must support recursive CTEs.

Keep the anchor projection and recursive projection compatible. Include the
fields required by the recursive join and the fields required by the final
result.

## 11. Functions and provider boundaries

Use registered functions for provider-specific behavior. A function
definition supplies its argument count, argument types, result type, and SQL
template for each supported dialect.

Do not embed schema-qualified names or raw SQL in field paths. Logical names
come from metadata; physical table names and provider syntax are generated by
the engine.

## 12. Validation checklist

Before saving a query definition, verify:

1. The root result grain is explicit.
2. Every context returns the grain expected by its consumer.
3. Contexts are declared in dependency order.
4. Every referenced context exists.
5. Context aliases are unique within the consuming query.
6. Projection aliases are not cyclic.
7. Alias filters and sorts occur after their aliases are materialized.
8. Paging is applied to the intended grain.
9. Functions are registered and have valid arguments.
10. Entities, fields, relations, and functions are authorized metadata.

Use the OQuery parser and generated SQL as validation steps. Do not validate
by assembling SQL manually.
