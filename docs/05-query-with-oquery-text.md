# Query with OQuery text

OQuery text is a readable form of the same query. It works well for saved
queries, screens that receive a query from a user, and reports.

```csharp
var rows = db.QueryOqr("""
User: Name, Manager.Name Manager-Name
  Where Name like <Dav%> AND Age >= <18>
  Order Manager.Name DESC
  Take 50
""").ToList();
```

## Structure

```text
Entity: Projection
  Where condition
  Order sort
  Skip n
  Take n
```

- The first line names the entity and lists the fields.
- Clauses are indented under that line. Keywords are not case-sensitive.
- Use metadata names only. Do not write table or schema names.

## Projection

```text
User: Id, Name, Manager.Name
User: Id, Name as DisplayName
User: Id, Name DisplayName
User: Id, Manager.Name | ManagerName
```

An alias can follow a field with a space, with `as`, or with `|`.

An entity name with no fields returns the fields in the metadata:

```text
User
```

## Values

Put values in angle brackets: `<...>`. Numbers can be written with or without
brackets.

```text
Where Status = <Completed>
Where Quantity > 10
Where IsActive = <true>
```

## Conditions

```text
Where Age >= <18> AND Name like <Dav%>
Where Status in <Paid,Shipped>
Where Id between <100,200>
Where Manager.Name is null
Where (Age < <12> OR Age > <65>) AND Active = <true>
```

Supported operators: `=`, `<>`, `>`, `<`, `>=`, `<=`, `like`, `not like`,
`between`, `not between`, `in`, `not in`, `is null`, and `is not null`.
`AND` and `OR` combine conditions, and parentheses group them.

## Sorting and paging

```text
User: Id, Name
  Order Name ASC
  Skip 0
  Take 25
```

`Order` takes a field and an optional `ASC` or `DESC`. Use `Skip` and `Take`
for paging.

## Run it

```csharp
var rows = db.QueryOqr(oqrText).ToList();
```

To inspect the generated SQL without running it, call `.ToSql()` on the same
result. See [Inspect SQL](./09-inspect-sql.md).

## Next step

Read about [Relationships](./06-relationships.md) to use paths such as
`Manager.Name`.
