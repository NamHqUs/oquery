# Query with C#

Use `db.Query("EntityName")` to start a query. Add operations, then run the
query with a result method such as `ToList()`. Nothing runs until you call a
result method.

```csharp
var users = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Id", "Name")
    .OrderBy("Name")
    .ToList();
```

Fields are read with `user["FieldName"]`. Use the logical name from metadata.

## Filter with Where

```csharp
var adults = db.Query("User")
    .Where(user => user["Age"] >= 18 && user["Name"].Like("Dav%"))
    .ToList();
```

- Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`.
- `Like("pattern")` for text patterns.
- Combine conditions with `&&` and `||`.

Values are sent to the database as parameters. Do not build filter text with
string concatenation.

## Choose columns with Select

```csharp
// A list of names
db.Query("User").Select("Id", "Name", "Manager.Name");

// Output names that differ from the field names
db.Query("User").Select(new Dictionary<string, object?>
{
    ["DisplayName"] = "Name",
    ["ManagerName"] = "Manager.Name",
});
```

If you do not call `Select`, the query returns the fields in the metadata.

## Sort and page

```csharp
var page = db.Query("User")
    .OrderByDescending("Age")
    .ThenBy("Name")
    .Skip(10)
    .TakeQuery(10)
    .ToList();
```

- `OrderBy`, `ThenBy`, `OrderByDescending`, and `ThenByDescending` take field names.
- `Skip(n)` skips rows. `TakeQuery(n)` limits rows and keeps the query open
  for more operations.
- `Take(n)` runs the query right away and returns a list.

## Check for rows

```csharp
int withName = db.Query("User").Count("Name");
bool hasManagers = db.Query("User").Any(user => user["ManagerId"] != null);
```

`Count` takes a field name. Use `Any` with a condition to check for matching rows.

## Next step

If you prefer a text form that you can save, see
[Query with OQuery text](./05-query-with-oquery-text.md).
