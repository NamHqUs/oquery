# Aggregates basics

An aggregate calculates one value from many rows, such as a count, a sum, or
an average. The standard aggregates are:

- `COUNT(expression)`
- `SUM(expression)`
- `AVG(expression)`
- `MAX(expression)`
- `MIN(expression)`

## Group by the non-aggregate fields

When a projection has both an aggregate and ordinary fields, OQuery groups the
rows by the ordinary fields. You do not write `GROUP BY`.

```text
Products: Id, Name, COUNT(OrderItems.Id) OrderCount
```

This returns one row per product, with the number of order items.

Use an alias to name the result column:

```text
OrderItems: ProductId, SUM(Quantity * UnitPrice) TotalRevenue
```

## Count and sum a relation

Use the relation name, then the aggregate, in C#:

```csharp
var totals = db.Query("User")
    .Select("Name", "Members.Count(Id)", "Members.Sum(Age)")
    .ToList();
```

Use `Count()` with no argument to count related rows. It renders `COUNT(1)`, so no
column is read.
`Sum`, `Avg`, `Min`, and `Max` need a numeric field.

`COUNT(*)` is not supported when written explicitly. Name a field, such as `COUNT(Id)`.

## Filter on an aggregate

An aggregate condition in `Where` runs after grouping, so the engine places it
in the `HAVING` part of the SQL:

```text
Products: Id, Name, COUNT(OrderItems.Id) OrderCount
  Where COUNT(OrderItems.Id) > <5>
```

This returns products with more than five order items.

## Next step

Read the values with [Results and async](./08-results-and-async.md).
