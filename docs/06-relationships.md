# Relationships

A relationship lets a query reach fields in another entity with a dotted
path. OQuery creates the join for you.

```csharp
db.Query("User").Select("Name", "Manager.Name");
```

```text
User: Name, Manager.Name
```

Both queries return each user's name and the name of their manager.

## Lookup: one related row

A lookup relation links to one row. Use it for a parent, such as a manager,
a department, or a country.

```csharp
new Relation("Manager", "User", RelationType.Lookup, [new("ManagerId", "Id")]);
```

Paths can go several levels deep when each step is a relation:

```text
Order: Id, Customer.Address.City
```

## Collection: many related rows

A collection relation links to many rows. Use it for children, such as members
of a team or items in an order.

```csharp
new Relation("Members", "User", RelationType.Collection, [new("Id", "ManagerId")]);
```

Selecting a collection field returns one row for each related row:

```text
User: Name, Members.Name MemberName
```

A user with three members returns three rows. To count or total the members
instead, use an aggregate. See [Aggregates basics](./07-aggregates-basics.md).

## Filter on a relation

Use a path in a condition:

```text
User: Id, Name
  Where Manager.Name = <Alice>
```

```csharp
db.Query("User").Where(user => user["Manager.Name"] == "Alice");
```

## Rules to remember

- Use the relation name in the path, not the table name.
- Each `RefKeys` pair must name a column on both sides.
- The engine rejects a path that names a missing field or relation. It does
  not guess a physical column.

## Next step

Summarize rows with [Aggregates basics](./07-aggregates-basics.md).
