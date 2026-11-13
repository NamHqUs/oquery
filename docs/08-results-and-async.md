# Results and async

Result methods run the query. Nothing runs before them.

## Dictionary rows

By default each row is a `Dictionary<string, object?>`. Keys are not case
sensitive.

```csharp
var users = db.Query("User").Select("Id", "Name").ToList();
var name = users[0]["Name"];
```

## Typed rows

Use a class whose public property names match the selected names. Values are
converted to the property type.

```csharp
public sealed class UserRow
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? Age { get; set; }
}

List<UserRow> users = db.Query("User")
    .Select("Id", "Name", "Age")
    .ToList<UserRow>();

UserRow? first = db.Query("User").FirstOrDefault<UserRow>();
```

## Common result methods

| Method | Returns |
|---|---|
| `ToList()` | All rows as dictionaries. |
| `ToList<T>()` | All rows as `T`. |
| `Take(n)` | Up to `n` rows, as a list. |
| `FirstOrDefault()` | The first row, or `null`. |
| `Count("Field")` | The number of rows that have a value in the field. |
| `Any(predicate)` | `true` if at least one row matches. |
| `ToListWithTotalRecords()` | The rows and the total count of matching rows. |

Total count:

```csharp
var (page, total) = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .OrderBy("Name")
    .Skip(10)
    .TakeQuery(10)
    .ToListWithTotalRecords();
```

The total is `-1` when more than 1,000 rows match. Use this as a signal to show
"more than 1,000" instead of an exact number.

## Async

Every result method has an async form that accepts a cancellation token:

```csharp
var users = await db.Query("User")
    .Where(user => user["Age"] >= 18)
    .ToListAsync(cancellationToken);

var first = await db.Query("User")
    .FirstOrDefaultAsync(cancellationToken);

var hasManagers = await db.Query("User")
    .AnyAsync(user => user["ManagerId"] != null, cancellationToken);
```

## Next step

To see the SQL behind a query, read [Inspect SQL](./09-inspect-sql.md).
