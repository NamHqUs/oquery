# Introduction

OQuery is a metadata-driven query library for .NET. You describe your tables,
columns, and relationships at runtime. OQuery turns a query written in C#,
OQuery text, or JSON into SQL for the database provider you connect to.

You do not need a generated entity class for each table. The same engine can
back a data grid, a report builder, an admin screen, or an API endpoint, even
when the fields are chosen at runtime.

## What you get

- Queries that use metadata names, not table classes.
- Three ways to write a query: the C# fluent API, OQuery text, and JSON.
- Relationship paths such as `Manager.Name`, with joins created for you.
- Parameterized values for every filter.
- Provider-specific SQL for the database you connect to.
- Support for .NET 8, .NET 9, and .NET 10.

## Core terms

| Term | Meaning |
|---|---|
| Entity | A logical data source, such as `User` or `Order`. It maps to a table. |
| Field | A logical column of an entity, such as `Name`. |
| Relation | A link to another entity. A lookup returns one row. A collection returns many rows. |
| Metadata provider | The object that holds your entities, fields, and relations. |
| `OQueryEngine` | The entry point. It owns the database connection and runs queries. |

## Three ways to write the same query

Each example finds users 18 or older, with their manager's name, sorted by name.

C# fluent API:

```csharp
var rows = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Name", "Manager.Name")
    .OrderBy("Name")
    .ToList();
```

OQuery text:

```text
User: Name, Manager.Name
  Where Age >= <18>
  Order Name ASC
```

JSON definitions are covered in the advanced part of this guide.

## Next step

Continue to [Quickstart](./02-quickstart.md) to run your first query.
