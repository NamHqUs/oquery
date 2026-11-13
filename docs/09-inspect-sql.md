# Inspect SQL

`ToSql()` shows the SQL that a query would run, with its parameters. It does
not run the query or open a result set.

```csharp
var sql = db.QueryOqr("""
User: Name
  Where Name like <Dav%> AND Age >= <18>
""").ToSql();

Console.WriteLine(sql.CommandText);

foreach (var parameter in sql.Parameters)
    Console.WriteLine($"{parameter.Name} = {parameter.Value}");
```

## What you see

- `CommandText` is the SQL statement. Physical table and column names are
  shown here, not in your query.
- `Parameters` lists the values. The SQL refers to them by name, for example
  `@p0`.
- For SQL Server, `CommandText` starts with typed `DECLARE` lines. You can
  paste the whole text into a query window.

## Handle the output with care

The output can contain user data, because values are part of the parameters.
Do not log it or show it to users who should not see the values.

## Next step

See which databases are supported in [Database providers](./10-providers.md).
