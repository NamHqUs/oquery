# Define metadata

Metadata tells OQuery what your database looks like. It does not create CLR
classes. You can build it in code, or load it from YAML or JSON in your
application and pass the result to `MetadataProvider`.

## Entity

An entity is a logical data source.

```csharp
new Entity(
    Name: "Student",
    DbSchema: "dbo",
    DbTable: "User",
    Fields:
    [
        new Field("Name", "Name"),
        new Field("Age", "Age"),
    ]);
```

| Property | Meaning |
|---|---|
| `Name` | The name queries use. Use a unique name. |
| `DbSchema` | Optional schema name. |
| `DbTable` | The physical table. If you leave it out, `Name` is used. |
| `Fields` | The fields that queries can use. |
| `Relations` | Optional links to other entities. |

Use the logical name in queries (`Student`). The physical table (`dbo.User`)
appears only in the generated SQL.

## Field

A field is a logical column.

```csharp
new Field("Name", "Name");
new Field("Age", "Age", DataType.Number);
```

| Parameter | Meaning |
|---|---|
| `Name` | The name queries use. |
| `DbColumn` | The physical column. If you leave it out, `Name` is used. |
| `DataType` | `String` (default), `Number`, `Datetime`, or `Boolean`. |
| `IsRawSql` | `true` for a calculated field. See the detail section. |

Set `DataType` so the query builder shows the right operators for the field.

## Relation

A relation links an entity to another entity.

```csharp
new Relation(
    Name: "Manager",
    RefEntity: "User",
    Type: RelationType.Lookup,
    RefKeys: [new("ManagerId", "Id")]);

new Relation(
    Name: "Members",
    RefEntity: "User",
    Type: RelationType.Collection,
    RefKeys: [new("Id", "ManagerId")]);
```

- `RelationType.Lookup` links to one row. A user has one manager.
- `RelationType.Collection` links to many rows. A user has many members.
- `RefKeys` lists the column pairs that join the two entities. Each pair is
  `(column on this entity, column on the related entity)`.
- Both sides of each pair must be set. Empty column names are not allowed.

Relations are used in paths such as `Manager.Name` and `Members.Name`. See
[Relationships](./06-relationships.md).

## Put it together

```csharp
var metadata = new MetadataProvider(
[
    new Entity(
        Name: "User",
        Fields:
        [
            new Field("Id", "Id"),
            new Field("Name", "Name"),
            new Field("Age", "Age", DataType.Number),
        ],
        Relations:
        [
            new Relation("Manager", "User", RelationType.Lookup, [new("ManagerId", "Id")]),
            new Relation("Members", "User", RelationType.Collection, [new("Id", "ManagerId")]),
        ]),
]);
```

## Next step

Write your first queries in C# with [Query with C#](./04-query-with-csharp.md).
