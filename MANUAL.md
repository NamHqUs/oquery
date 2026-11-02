# OQuery Manual

## Overview

`OQuery` is a metadata-driven .NET query library. It executes queries
through ADO.NET while allowing entities, fields, relationships, projections,
filters, and aggregates to be defined at runtime.

### Filter operators

The metadata builder exposes read-only built-in filter operators and supports
custom operators per user section. Operators declare the virtual data types
they support (`String`, `Number`, `Datetime`, or `Boolean`), so the query
builder only presents compatible operators for the selected field or
calculation.

Custom operators provide one SQL template for each supported provider. Use
exactly one `{left}` placeholder for the selected field and one `{value}`
placeholder for the single filter input value, for example:

```text
LOWER({left}) LIKE LOWER({value})
```

The filter input remains parameterized; only the operator template is stored
as SQL metadata. Built-in operators are read-only, while custom operators can
be created, edited, and deleted through the metadata builder.

The library targets .NET 8, .NET 9, and .NET 10.

## Installation

Add the NuGet package, or reference the projects while developing locally:

```xml
<ItemGroup>
  <PackageReference Include="OQuery" Version="..." />
</ItemGroup>
```

The model types are included through the library package. A local project
reference looks like this:

```xml
<ProjectReference Include="path\to\src\OQuery\OQuery.csproj" />
```

## Define metadata

Metadata describes the database without requiring CLR entity classes:

```csharp
using OQuery;

var metadata = new[]
{
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
            new(
                Name: "Manager",
                RefEntity: "User",
                Type: RelationType.Lookup,
                RefKeys: [new("ManagerId", "Id")]),
            new(
                Name: "Members",
                RefEntity: "User",
                Type: RelationType.Collection,
                RefKeys: [new("Id", "ManagerId")]),
        ])
};

var metadataProvider = new MetadataProvider(metadata);
```

Metadata can also be loaded from the YAML array format used by the demo
metadata files:

```csharp
var metadataProvider = MetadataProvider.FromYmlFile("metadata.yml");
```

### Entities

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

`Name` is the logical entity name used by queries and relationships.
`DbSchema` and `DbTable` identify a normal physical table and are quoted by the SQL dialect.
For a view-like entity backed by a calculated or joined SQL statement, set
`IsRawSql` to `true` and provide the SQL source through `DbTable`:

```csharp
new Entity(
    Name: "Calculation",
    DbTable: "(SELECT Id, Name, Amount * Quantity AS Total FROM OrderLines)",
    IsRawSql: true,
    Fields: [
        new Field("Id"),
        new Field("Name"),
        new Field("Total")
    ]);
```

When `Entity.IsRawSql` is true, `Entity.DbTable` is the trusted SQL source rendered in the
`FROM` clause. A field with `IsRawSql` set to `true` uses `DbColumn` as its trusted SQL
expression. Its fields and relations are
available to the query compiler in the same way as table-backed entity metadata.
The built-in SQL dialects reject configured DML/DDL keywords in this source after
ignoring literals, quoted identifiers, and comments. This is a conservative
blacklist check, not a SQL parser or a security boundary; keep raw SQL metadata
trusted and use read-only database permissions where applicable.

### Metadata permissions

Field access can be configured with `PermissionManagement` and supplied to the
metadata provider. Permissions are checked when the SQL generator renders each
resolved field, before the field becomes part of the SQL statement. This
covers fields in projections, filters, sorting, grouping, joins, CTEs, and
subqueries without adding permission work to every visitor operation.

```csharp
var permissions = new PermissionManagement(
    [
        new MetadataPermission("User", ["Id", "DisplayName"]),
        new MetadataPermission("AuditLog", null),
    ],
    IsDenied: false);

var metadataProvider = new MetadataProvider(metadata, permissions);
```

`IsDenied: false` makes the configured fields an allow-list. `IsDenied: true`
makes them a deny-list. A null `Fields` list creates an `EntityName\0*`
wildcard, which applies to every field of that entity. Permission keys are
stored in one case-insensitive hash set; the wildcard is checked before the
field-specific key. Relations are used to resolve field paths but are not
permission-checked.

Permissions can also be loaded from YAML:

```csharp
var permissions = PermissionManagement.FromYml("""
    - entityName: User
      fields:
      - Id
      - DisplayName
    - entityName: AuditLog
    """);
```

Use `PermissionManagement.FromYmlFile(filePath)` when loading permissions
from a file.

### Fields

Fields can be:

- `Field`: a database column. `Name` is the logical field name and
  `DbColumn` is its physical database column name.
- A raw SQL `Field` has `IsRawSql` set to `true`; its `DbColumn` stores a trusted
  provider-specific SQL expression.

Use `@` before column names in a raw SQL expression to have the current table
alias inserted. For example, this defines a calculated `LineTotal` field
without requiring a physical column:

```csharp
new Field("LineTotal", "@Quantity * @UnitPrice", IsRawSql: true)
```

The generated SQL expression will be similar to
`(t0.Quantity * t0.UnitPrice)`, where `t0` is the generated table alias.
Raw SQL is inserted into the query, so only use trusted metadata; never build
it from user-supplied input.

### Relationships

Use `RelationType.Lookup` for one related row and
`RelationType.Collection` for one-to-many traversal:

```csharp
new Relation(
    Name: "School",
    RefEntity: "School",
    Type: RelationType.Lookup,
    RefKeys: [new("SchoolId", "Id")]);
```

## Create a context

The context owns and disposes the supplied connection:

```csharp
using Microsoft.Data.SqlClient;

using var db = new OQueryEngine(
    new SqlConnection(connectionString),
    metaProvider);
```

The dialect is inferred from the connection type. It can also be supplied
explicitly:

```csharp
using var db = new OQueryEngine(
    connection,
    metaProvider,
    new SqliteDialect(schema: "dbo"));
```

Built-in dialects are `SqlServerDialect`, `SqliteDialect`, `PostgresDialect`,
`CockroachDialect`, `FirebirdDialect`, `MariaDialect`, `OracleDialect`,
`SapHanaDialect`, and `Db2Dialect`. PostgreSQL and CockroachDB use the
`public` schema by default.

CockroachDB uses the Npgsql connection type, like PostgreSQL, so a generic
`OQueryEngine(connection, metaProvider)` cannot distinguish the two and
selects the PostgreSQL dialect. When connecting to CockroachDB directly,
provide the dialect explicitly or use `CockroachOQueryEngine`:

```csharp
using var db = new OQueryEngine(
    connection,
    metaProvider,
    new CockroachDialect());

// Equivalent:
using var db = new CockroachOQueryEngine(connection, metaProvider);
```

Dialects control identifier quoting, booleans, string concatenation, and
paging. Raw SQL and aggregate expressions must use syntax supported by the
configured provider.

## C# queries

Results are returned as `Dictionary<string, object?>`, a case-insensitive field/value map:

```csharp
var users = db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Select("Id", "Name", "Manager.Name")
    .OrderBy("Name")
    .ToList();
```

```csharp
foreach (var user in users)
    Console.WriteLine($"{user["Id"]}: {user["Name"]}");
```

For a declared result type, use the generic materialization extensions. The
selected field aliases must match writable public property names
(case-insensitively), and database values are converted to the property type:

```csharp
public sealed class UserResult
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? Age { get; set; }
}

var users = db.Query("User")
    .Select("Id", "Name", "Age")
    .ToList<UserResult>();

var usersAsync = await db.Query("User")
    .Select("Id", "Name", "Age")
    .ToListAsync<UserResult>();
```

The same materialization is available through
`FirstOrDefault<T>()` and `ToListWithTotalRecords<T>()` (including their
asynchronous counterparts). Existing non-generic methods continue to return
`Dictionary<string, object?>`.

### Filters

```csharp
var users = db.Query("User")
    .Where(user =>
        user["Age"] >= 18 &&
        user["Name"].Like("Dav%"))
    .ToList();
```

### Projections and aliases

```csharp
var users = db.Query("User")
    .Select(new Dictionary<string, object?>
    {
        ["Id"] = "Id",
        ["DisplayName"] = "Name",
        ["ManagerName"] = "Manager.Name",
    })
    .ToList();
```

Computed projections can use related queries:

```csharp
var users = db.Query("User")
    .Select(user => new Dictionary<string, object?>
    {
        ["Name"] = user["Name"],
        ["MemberCount"] = user.Query("Members").Count(),
    })
    .ToList();
```

### Relationships and subqueries

```csharp
var managers = db.Query("User")
    .Where(user => user.Query("Members")
        .Any(member => member["Age"] > 30))
    .Select("Name", "Members.Name")
    .ToList();

var activeManagers = db.Query("User")
    .Where(user => user.Query("Members").Count() >= 2)
    .ToList();
```

### Ordering and paging

```csharp
var page = db.Query("User")
    .Where(user => user["IsMale"] == true)
    .OrderByDescending("Age")
    .ThenBy("Name")
    .Skip(10)
    .TakeQuery(10)
    .ToList();
```

`TakeQuery` keeps paging composable. `Take` materializes a list.

To fetch a page together with the total number of matching records before
paging, use `ToListWithTotal`. The total is `-1` when more than 1,000 records
match:

```csharp
var query = db.Query("User")
    .Where(user => user["IsMale"] == true)
    .OrderByDescending("Age")
    .Skip(10)
    .TakeQuery(10);

var (pageRecords, resultTotal) = query.ToListWithTotal(out var totalRecords);
```

### Scalar operations

```csharp
var totalUsers = db.Query("User").Count();
var adults = db.Query("User").Count(user => user["Age"] >= 18);
var hasManagers = db.Query("User")
    .Any(user => user["ManagerId"] != null);
```

### Aggregates

Define aggregates in the query projection with functions such as `COUNT()` and
`SUM(Age)`. Query expressions can also aggregate over related collections, for
example `Members.COUNT()`, without adding a synthetic aggregate field to entity
metadata.

## YAML queries

Use `QueryYml` when a screen, API, or report supplies query rules at runtime:

```csharp
var rows = db.QueryYml("""
Entity: User
Projection:
  - Name
  - Manager.Name | Manager-Name
Filter: "[Name] like 'Dav%' && ([Manager.Name] == 'Alice' || [Age] < 12)"
Sorts: Manager.Name DESC
Page: 1
PageSize: 50
""").ToList();
```

Supported properties:

- `entity`: metadata entity name.
- `projection`: projection expressions; use `|` to specify an output alias.
- Legacy YAML and JSON definitions using `Fields` remain readable.
- Saved queries use `QueryDto`: `MainQuery` stores the root query and
  `Contexts` stores each distinct context once. `MainQuery.ExtraRelations`
  references context IDs rather than embedding query copies. Saved-query
  files are named with `MainQuery.Id`.
- Saved-query projections on `QueryContextDto` are stored as a flat list.
  The reserved alias `NEW_LINE` marks query-designer row boundaries; those
  markers are retained in saved-query files and ignored when creating a
  `QueryDefinition`, so they do not affect query execution.
- Saved-query `Filters` contains one recursive filter group or `null`; older
  arrays of filter groups are combined with `AND` when read.
- `filter`: field comparisons, `like`, `not like`, `between`, `not between`,
  `in`, `not in`, `is null`, `is not null`, `&&`, `||`, `AND`, `OR`, and
  parentheses. Multiple values use angle brackets with comma-separated values,
  for example `Id between <100,200>` or `Status in <Paid,Shipped>`. The query
  designer accepts the same values without the angle brackets, such as
  `100,200`.
- `sorts`: comma-separated field expressions with an optional `ASC` or `DESC`
  direction (for example, `Manager.Name DESC`).
- `page`: one-based page number.
- `pageSize`: positive number of rows per page. If omitted or set to `0`, paging
  is disabled; when enabled, an omitted `page` defaults to `1`.

Bare words on the right side of a filter comparison are accepted as string
values (for example, `Status == Paid`). Filter values remain parameterized by
the query engine, so the active provider handles their SQL representation.
Plan-text filters use `=`/`<>`, quote string values with double quotes, and
write right-side field references in brackets; this text can be parsed back
into the same filter structure.

Field references in filters use brackets:

```text
[Name]
[Manager.Name]
[Members.COUNT()]
```

Values may be strings, integers, decimals, booleans, or `null`. JSON filter
values are parameterized by the query pipeline.

Calculated projections use a structured expression list rather than SQL text.
Supported nodes are `field`, numeric `value`, `group`, and `function`
(`COUNT`, `SUM`, `AVG`, `ROUND`, `NULLIF`,
`DateSub`, `MonthSub`, `YearSub`, `DateAdd`, `MonthAdd`, `YearAdd`,
`DateFormat`, `GETDATE`):

```json
{
  "entity": "Products",
  "projection": [
    {
      "alias": "GrossProfit",
      "fields": [
        {
          "kind": "function",
          "function": "SUM",
          "arguments": [
            {
              "kind": "group",
              "fields": [
                {
                  "kind": "field",
                  "field": "InvoiceItems.LineTotal"
                },
                {
                  "oper": "-",
                  "kind": "group",
                  "fields": [
                    {
                      "kind": "field",
                      "field": "InvoiceItems.Quantity"
                    },
                    {
                      "oper": "*",
                      "kind": "field",
                      "field": "CostPrice"
                    }
                  ]
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  "sorts": [
    "-GrossProfit"
  ]
}
```

Related field references use metadata relation paths, so the query compiler
creates the necessary joins. Numeric expression values are parameterized.
Each projection contains a flat `fields` array with no root group. The `oper`
property belongs to each field/value/function/group node and defaults to `+`;
parenthesized terms and arithmetic function arguments use nested `group`
nodes. The compiler applies arithmetic precedence to flat terms.
For examples of the canonical serialized shape, see the
[Query Model JSON Reference](./QUERY-MODEL-REFERENCE.md).
Function calls are validated against definitions in the metadata provider.
The default provider includes the built-in functions shown above. Applications can add or override definitions with `FunctionDefinition`.
Each definition specifies its UI-facing arguments, result type, and SQL
templates keyed by the dialect's stable string identifier (`ISqlDialect.Key`).
Every supported dialect must have a template; calling a function with an
unsupported dialect fails explicitly. Templates are trusted metadata, not
query input. They use 1-based argument placeholders
(`{1}`, `{2}`, ...), while query values remain parameterized.
The result type is a logical `DataType` (`String`, `Number`, `Datetime`, or
`Boolean`), not a CLR numeric precision such as `int` or `double`; the engine
maps it to an internal CLR expression type when needed.
`FunctionDefinition.Description` is required and provides UI-friendly help
text for each function, and `FunctionDefinition.Arguments` describes its
inputs. Each argument has a name and a `DataType`; all arguments accept query
expressions, including constants. A default value makes an argument optional.
The special default `*` is emitted as a SQL token; other defaults must match
the declared data type. Templates can refer to arguments by 1-based position
(`{1}`, `{2}`, ...) or by name (`{value}`); query values remain parameterized.
Built-in date helpers use readable names while their templates use each
provider's native SQL:

```csharp
var daysBetween = new FunctionFieldDto("DateSub",
[
    new FieldDto("InvoiceDate"),
    new FieldDto("PaidOn")
]);

var dueInTwoMonths = new FunctionFieldDto("MonthAdd",
[
    new FieldDto("DueDate"),
    new ConstFieldDto(2)
]);

var monthLabel = new FunctionFieldDto("DateFormat",
[
    new FieldDto("InvoiceDate"),
    new ConstFieldDto("%Y-%m")
]);
```

`DateSub(startDate, endDate)` returns elapsed whole days;
`MonthSub` and `YearSub` return calendar-boundary differences. `DateAdd`,
`MonthAdd`, and `YearAdd` add the specified number of units to a date.
`DateFormat` is currently SQLite-specific. The provider-specific SQL remains internal; for
example, `DateSub` uses `DATEDIFF(day, ...)` on SQL Server.

`FunctionProvider.GetFunctions()` exposes the registered definitions for
applications that build a function-selection UI.

Adding a database provider does not require changes to a fixed dialect enum.
Implement `ISqlDialect` with a unique `Key`, then supply or override metadata
definitions for functions whose syntax differs. For example, an
Oracle-specific function can be registered as:

```csharp
var toDate = new FunctionDefinition(
    name: "TO_DATE",
    arguments: [new FunctionArgumentDefinition("value", DataType.String)],
    resultType: DataType.Datetime,
    description: "Parses a date from a string.",
    sqlTemplates: new Dictionary<string, string>
    {
        ["Oracle"] = "TO_DATE({1}, 'YYYY-MM-DD')"
    });

var metadata = new MetadataProvider(entities);
var functionProvider = new FunctionProvider(new SqlServerDialect(), [toDate]);
```

For example, an application can expose a one-argument `TODAY(value)` wrapper
and keep the SQL cast fixed in the templates:

```csharp
var today = new FunctionDefinition(
    name: "TODAY",
    arguments: [new FunctionArgumentDefinition("value", DataType.Datetime)],
    resultType: DataType.Datetime,
    description: "Returns the date portion of a value.",
    sqlTemplates: new Dictionary<string, string>
    {
        ["SqlServer"] = "CAST({1} AS DATE)",
        ["Sqlite"] = "CAST({1} AS DATE)",
        ["Postgres"] = "CAST({1} AS DATE)",
        ["Cockroach"] = "CAST({1} AS DATE)"
    });

var metadata = new MetadataProvider(entities);
var functionProvider = new FunctionProvider(new SqlServerDialect(), [today]);
using var context = new OQueryEngine(connection, metadata, functionProvider, dialect);
```

The function call still receives an expression DTO, such as
`new FunctionFieldDto("TODAY", [new FieldDto("InvoiceDate")])`. Nested
functions and arithmetic expressions can be supplied as arguments. Built-in
date function templates map friendly function names to provider-specific SQL,
and `GETDATE()` renders the provider's current date and time.

## YAML query definitions

`QueryDefinitionParser.ParseFromYml` accepts YAML text or a `FileInfo`:

```csharp
var query = QueryDefinitionParser.ParseFromYml("""
Name: Product totals
Entity: InvoiceItems
Projection:
  - Product.Name|Product
  - Count()|ItemCount
Sorts: ItemCount DESC
""");

var fileQuery = QueryDefinitionParser.ParseFromYml(new FileInfo("query.yml"));
```

YAML query definitions use the same properties as `QueryDefinition`. Sorts are
plain-text expressions parsed by the sort definition parser. Projection and
filters use the existing query expression syntax. Projection is a list of
expressions, and sorts are a comma-separated string with an optional `ASC` or
`DESC` direction.

For the canonical JSON projection-expression and `Contexts` shapes, see the
[query model reference](QUERY-MODEL-REFERENCE.md).

### Common table expressions

`QueryDefinition.Contexts` defines named CTEs. Each context has a query, an
optional join condition, and a join type. The join type defaults to `Left`;
contexts without a condition are declared as CTEs but are not joined. The
outer `Entity` may name either a metadata entity or a CTE, so a query can use a
CTE as its `FROM` source:

```yaml
Entity: A
Projection:
  - A.Salary
  - B.MinSalary
Contexts:
  - Name: A
    Query:
      Entity: Employees
      Projection:
        - Id
        - Salary
  - Name: B
    JoinType: Inner
    Query:
      Entity: SalaryRules
      Projection:
        - Id
        - MinSalary
    Condition: A.Salary BETWEEN <B.MinSalary, B.MinSalary + 10000>
```

For saved `QueryDto` requests, `MainQuery.Entity` may match a reference
context's `Name`. That context is the root source, not an extra join: keep its
definition in `Contexts` and leave `MainQuery.ExtraRelations` empty unless
there is a separate join. When converting the request to a `QueryDefinition`,
include the source context in `QueryDefinition.Contexts` with no alias or join
condition. The compiler then uses the context CTE as `FROM` without emitting a
`JOIN`.

For a condition, unqualified left-side fields resolve against the outer source
and unqualified right-side fields resolve against the joined context. Prefix a
field with a CTE name (for example, `A.Salary`) to select a specific source.
Arithmetic expressions and range/set conditions are supported; literal values
remain SQL parameters. In JSON context conditions, an omitted `operation`
defaults to the built-in Equals (`=`) operator. CTE names must be unique within the query, and a CTE must appear before any
context query that uses it as its source. Recursive contexts are compiled as
recursive CTEs when the selected dialect supports them. Contexts nested inside
another context query remain unsupported.

## SQL diagnostics

Inspect generated SQL without executing it:

```csharp
var sql = db.QueryYml(yml).ToSql();

Console.WriteLine(sql.CommandText);

foreach (var parameter in sql.Parameters)
    Console.WriteLine($"{parameter.Name} = {parameter.Value}");
```

For SQL Server, `CommandText` starts with typed declarations:

```sql
DECLARE @p0 nvarchar(4000) = 'Dav%',
	@p1 nvarchar(4000) = 'Alice',
	@p2 int = 12;

SELECT ...
```

This output can be copied directly into a SQL Server query window. Other
dialects include a trailing parameter comment.

Treat diagnostic output as sensitive because it may contain user data.

## Async APIs

Async list, first-row, count, and existence operations accept cancellation:

```csharp
var users = await db.Query("User")
    .Where(user => user["Age"] >= 18)
    .ToListAsync(cancellationToken);

var (pageRecords, totalRecords) = await db.Query("User")
    .Where(user => user["Age"] >= 18)
    .Skip(10)
    .TakeQuery(10)
    .ToListWithTotalAsync(cancellationToken);

var firstUser = await db.Query("User")
    .FirstOrDefaultAsync(cancellationToken);

var adultCount = await db.Query("User")
    .CountAsync(user => user["Age"] >= 18, cancellationToken);

var hasManagers = await db.Query("User")
    .AnyAsync(user => user["ManagerId"] != null, cancellationToken);
```

## Query security

Query values are passed as parameters; the C# query API does not expose
methods for inserting raw SQL fragments. SQL expressions in trusted metadata,
such as aggregate definitions, must not be derived from untrusted input.

## Build and test

```powershell
dotnet build .\src\OQuery.UnitTest\OQuery.UnitTest.csproj
dotnet test .\src\OQuery.UnitTest\OQuery.UnitTest.csproj
```

SQL Server tests expect a local `DynamicSqlTest` database. SQLite, PostgreSQL,
and CockroachDB scripts are included in `data-storage/scripts`.

## License

Copyright (c) 2026 NamHqUs

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
