# Quickstart

This page runs a small query from start to finish. It uses an in-memory list
of metadata, so you can see the whole flow in one place.

## 1. Add the package

```powershell
dotnet add package OQuery
```

## 2. Describe the metadata

```csharp
using OQuery;

var metadata = new MetadataProvider(
[
    new Entity(
        Name: "User",
        Fields:
        [
            new Field("Id", "Id"),
            new Field("Name", "Name"),
            new Field("Age", "Age"),
        ],
        Relations:
        [
            new Relation(
                Name: "Manager",
                RefEntity: "User",
                Type: RelationType.Lookup,
                RefKeys: [new("ManagerId", "Id")]),
        ]),
]);
```

## 3. Create the engine

```csharp
using Microsoft.Data.SqlClient;

using var db = new OQueryEngine(
    new SqlConnection(connectionString),
    metadata);
```

The engine owns the connection and disposes it when you dispose the engine.
The provider is chosen from the connection type. See
[Database providers](./10-providers.md).

## 4. Run a query

```csharp
var users = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select(new Dictionary<string, object?>
    {
        ["Id"] = "Id",
        ["Name"] = "Name",
        ["ManagerName"] = "Manager.Name",
    })
    .OrderBy("Name")
    .ToList();

foreach (var user in users)
    Console.WriteLine($"{user["Name"]} reports to {user["ManagerName"]}");
```

Each row is a `Dictionary<string, object?>`. Keys are the output names you
chose in `Select`.

## 5. Look at the SQL (optional)

```csharp
var sql = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Id", "Name", "Manager.Name")
    .ToSql();

Console.WriteLine(sql.CommandText);
```

`ToSql()` does not run the query. See [Inspect SQL](./09-inspect-sql.md).

## Next step

Learn how to describe your data in [Define metadata](./03-define-metadata.md).
