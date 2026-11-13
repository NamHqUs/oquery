# Security basics

OQuery is built so that user input cannot change the SQL structure. Values are
sent as parameters. Metadata decides which tables and columns exist. You still
need to control which metadata users can reach.

## Values are parameters

Filter values, such as `<Alice>` or `18`, become database parameters. The
C# query API has no method that inserts raw SQL text from user input.

## Limit fields with an authorizer

By default, `MetadataProvider` allows every field. Apps that take queries
from users should always pass an authorizer:

```csharp
var authorizer = new MetadataFieldAuthorizer(
[
    new MetadataFieldPermission("User", ["Id", "Name"]),
    new MetadataFieldPermission("AuditLog", null),
],
accessMode: AccessMode.Allow);

var metadata = new MetadataProvider(entities, authorizer);
```

- `AccessMode.Allow` allows only the listed fields.
- `AccessMode.Deny` allows everything except the listed fields.
- A `null` field list covers every field of that entity.

The engine checks every field a query uses. It rejects a query that uses a
field the user cannot access.

## Treat raw SQL as configuration

Metadata can contain raw SQL, for example a calculated field or a view-like
entity. Only write this metadata yourself. Never build it from user input.

The engine blocks some DML and DDL keywords in raw SQL sources. This is a
safety check, not a SQL parser. Use database accounts with read-only rights
for query execution where you can.

## Protect diagnostic output

`ToSql()` output can include values from users. Keep it out of logs and
responses that users are not allowed to see.

## Next step

You have finished the simple path. Detail and advanced topics are planned.
