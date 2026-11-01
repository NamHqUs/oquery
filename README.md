# OQuery

Build powerful database screens without creating a CLR class for every table.

`OQuery` turns runtime metadata, C# expressions, or YAML definitions into
safe, provider-specific SQL. The same query engine can power a data grid,
report builder, admin screen, or API endpoint even when the fields and
relationships are selected at runtime.

## A query defined by data

```csharp
var rows = db.QueryYml("""
Entity: User
Projection:
  - Name
  - Manager.Name | Manager-Name
Filter: "[Name] like 'Dav%' && [Age] >= 18"
Sorts: Manager.Name DESC
Page: 1
PageSize: 50
""").ToList();
```

No generated entity class. No hand-written join. No string-built SQL.

The same metadata can be queried from C#:

```csharp
var rows = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Name", "Manager.Name")
    .ToList();
```

## Why it is different

- Runtime entities, fields, relationships, and aggregates.
- One compiler for C# expressions and YAML queries.
- Relationship paths such as `Manager.Name` and `Members.Name`.
- Dynamic projections and aliases without CLR models.
- SQL Server, SQLite, PostgreSQL, CockroachDB, Firebird, MySQL/MariaDB, Oracle,
  SAP HANA, and IBM Db2 dialect support.
- Parameterized filters and executable SQL diagnostics.
- A dialect-independent SQL expression intermediate representation.

```csharp
var sql = db.QueryYml(yml).ToSql();
Console.WriteLine(sql.CommandText);
```

For SQL Server, the diagnostic output includes typed `DECLARE` statements, so
the complete text can be copied directly into a query window.

## Start here

- Read the [user manual](./MANUAL.md) for installation, metadata, queries, YAML,
  providers, async APIs, and troubleshooting.
- Read the [technical guide](./TECH.md) for the compiler pipeline and important
  classes.
- See the [query model reference](./QUERY-MODEL-REFERENCE.md) for the
  canonical backend JSON format for projections and query contexts.

Targets: **.NET 8, .NET 9, and .NET 10**.

## Build

Build the core library and unit-test project with:

```powershell
dotnet build .\src\OQuery.slnx
```

## License

MIT License. See [the manual](./MANUAL.md#license) for the full license text.
