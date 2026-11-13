# Database providers

OQuery builds SQL for the database you connect to. The same metadata and
queries work with each provider. The provider handles quoting, booleans,
string concatenation, and paging.

## Supported providers

| Provider | Dialect class |
|---|---|
| SQL Server | `SqlServerDialect` |
| SQLite | `SqliteDialect` |
| PostgreSQL | `PostgresDialect` |
| CockroachDB | `CockroachDialect` |
| Firebird | `FirebirdDialect` |
| MySQL / MariaDB | `MariaDialect` |
| Oracle | `OracleDialect` |
| SAP HANA | `SapHanaDialect` |
| IBM Db2 | `Db2Dialect` |

## Choose a dialect

Most of the time you do not need to choose. OQuery reads the connection type:

```csharp
using var db = new OQueryEngine(connection, metadata);
```

To set it yourself, pass the dialect:

```csharp
using var db = new OQueryEngine(connection, metadata, new SqliteDialect(schema: "dbo"));
```

## PostgreSQL and CockroachDB

Both use the same connection type, so the generic constructor chooses
PostgreSQL. For CockroachDB, pass the dialect or use the Cockroach engine:

```csharp
using var db = new CockroachOQueryEngine(connection, metadata);
```

PostgreSQL and CockroachDB use the `public` schema by default.

## Next step

Read the [Security basics](./11-security-basics.md) before you expose queries
to users.
