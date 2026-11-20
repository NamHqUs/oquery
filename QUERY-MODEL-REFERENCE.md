# Query model JSON reference

This document defines the current canonical JSON contract for
`QueryDefinition`. It describes the serialized model, not OQuery authoring
syntax or compiler internals.

- Use [OQUERY-SYNTAX.md](./OQUERY-SYNTAX.md) for OQuery text.
- Use [OQUERY-ENGINE.md](./OQUERY-ENGINE.md) for semantic modeling and
  execution invariants.

The model types are defined under
[`src/OQuery.Abstracts/Contracts/QueryDefinitions`](./src/OQuery.Abstracts/Contracts/QueryDefinitions).
JSON conversion is implemented by
[`QueryDefinitionJsonParser`](./src/OQuery.Core/Contracts/QueryParsers/JsonParsers/QueryDefinitionJsonParser.cs).

## 1. Top-level query definition

```json
{
  "entity": "Products",
  "projection": [
    {
      "field": {
        "kind": "field",
        "field": "Id"
      },
      "alias": null
    }
  ],
  "filter": null,
  "sorts": null,
  "take": null,
  "skip": null,
  "relations": null,
  "contexts": {}
}
```

Properties:

- `entity` is the logical source entity or source context name.
- `projection` is a list of `ProjectionDefinition` values.
- `filter` is a condition tree applied to the query.
- `sorts` is a flat list of sort definitions; `null` means no ordering.
- `take` limits rows after the query result is formed.
- `skip` skips rows before `take`.
- `relations` lists context references consumed by this query.
- `contexts` maps context names to `QueryDefinitionBase` values.

`QueryDefinitionBase` contains `entity`, `projection`, `filter`, `sorts`,
`take`, `skip`, and `relations`. Only the top-level `QueryDefinition` owns
`contexts`.

The execution meaning of an empty `projection` and the `*` field is defined
centrally in [OQUERY-ENGINE.md](./OQUERY-ENGINE.md). This document only
defines how those values are represented in JSON.

## 2. Projection definitions

A projection contains a `field` node and an optional `alias`:

```json
{
  "field": {
    "kind": "function",
    "function": "Sum",
    "arguments": [
      {
        "kind": "field",
        "field": "Amount"
      }
    ]
  },
  "alias": "TotalAmount"
}
```

Aliases are scoped to the current context. The engine resolves all aliases in
that scope independently of their output position, then preserves the
requested projection order.

## 3. Field nodes

### Field

```json
{
  "kind": "field",
  "field": "Customer.Name"
}
```

`field` contains a logical field or relation path. Physical schema names do
not belong in this property.

The special field path `*` is serialized as a normal field node. Its execution
semantics are defined in [OQUERY-ENGINE.md](./OQUERY-ENGINE.md), not by the
JSON serializer.

### Value

```json
{
  "kind": "value",
  "value": 10
}
```

Values may be strings, numbers, booleans, or null. The query compiler
parameterizes values for execution.

### Function

```json
{
  "kind": "function",
  "function": "Round",
  "arguments": [
    {
      "kind": "field",
      "field": "Amount"
    },
    {
      "kind": "value",
      "value": 2
    }
  ]
}
```

Function names and arguments are validated against registered
`SqlFunctionDefintion` metadata.

### Group

```json
{
  "kind": "group",
  "fields": [
    {
      "kind": "field",
      "field": "Quantity"
    },
    {
      "oper": "*",
      "kind": "field",
      "field": "UnitPrice"
    }
  ]
}
```

`fields` is a flat expression sequence. Each later node uses `oper` to
describe how it combines with the preceding expression. A group preserves
parentheses.

### Condition expression

Conditional functions use a condition node:

```json
{
  "kind": "condition",
  "condition": {
    "logicalOperation": 0,
    "filters": [
      {
        "logicalOperator": null,
        "left": {
          "kind": "field",
          "field": "IsActive"
        },
        "operation": 0,
        "right": {
          "kind": "value",
          "value": true
        },
        "parameterized": true
      }
    ]
  }
}
```

The condition model is shared by query filters and conditional function
arguments.

## 4. Operators

`oper` is omitted for the first or default `Add` node. Supported arithmetic
and bitwise operators are:

| Symbol | Meaning |
|---|---|
| `+` | Add |
| `-` | Subtract |
| `*` | Multiply |
| `/` | Divide |
| `%` | Modulus |
| `&` | Bitwise AND |
| `|` | Bitwise OR |

Condition `operation` values use the `BinaryOperator` enum:

| Value | Operator |
|---:|---|
| `0` | Equals |
| `1` | NotEquals |
| `2` | Like |
| `3` | NotLike |
| `4` | GreaterThan |
| `5` | LessThan |
| `6` | GreaterThanOrEqual |
| `7` | LessThanOrEqual |
| `8` | Between |
| `9` | NotBetween |
| `10` | In |
| `11` | NotIn |
| `12` | IsNull |
| `13` | IsNotNull |

`logicalOperation` and `logicalOperator` use `LogicalOperator`: `0` is `And`
and `1` is `Or`.

### Collection predicate condition

`Any` and `NotAny` are Boolean collection predicates. They use the same
function shape as projections, and the first argument names the collection
path. An optional second argument is an alias, and an optional last argument is
the predicate text:

```json
{
  "kind": "function",
  "function": "NotAny",
  "arguments": [
    { "kind": "field", "field": "Users" },
    { "kind": "field", "field": "Customer" },
    { "kind": "field", "field": "Customer.Status = <Active>" }
  ]
}
```

The equivalent OQuery text is `NotAny(Users, Customer, Customer.Status = <Active>)`.
The dotted form `Users.NotAny(Status = <Active>)` is the same condition: the
receiver becomes the first argument. `NotAny` renders `NOT EXISTS`; `Any`
renders `EXISTS`. The predicate is evaluated in the collection's scope.

## 5. Relations

A relation references a context by dictionary key:

```json
{
  "refContext": "ProductSales",
  "alias": null,
  "condition": null,
  "joinType": "Left"
}
```

Properties:

- `refContext` is required and must match a key in `contexts`.
- `alias` is the address used for this relation instance.
- `condition` describes how the consuming query joins the context.
- `joinType` is `Inner`, `Left`, `Right`, or `Full`.

The model default for `joinType` is `Left` when a `JoinDefinition` is created
without an explicit value. Serialization preserves an explicit null.

The same context may be referenced by multiple relation entries. Each entry
must use a distinct `alias` when the consuming query needs separate relation
instances:

```json
{
  "refContext": "UserContext",
  "alias": "Manager1",
  "condition": "ManagerId = Id",
  "joinType": "Left"
}
```

```json
{
  "refContext": "UserContext",
  "alias": "Manager2",
  "condition": "ManagerId = Id",
  "joinType": "Left"
}
```

This represents two aliases of the same context and is supported by the query
model and SQL generation. It should not be confused with a correlated
subquery, where an inner relation is evaluated per outer row and compares
against the outer relation instance; that requires separate subquery support.

## 6. Context graph

Contexts are stored once in a dictionary. The key is the context identity;
the value is a `QueryDefinitionBase` and does not repeat the name:

```json
{
  "entity": "Products",
  "projection": [],
  "relations": [
    {
      "refContext": "ProductSales",
      "condition": null,
      "joinType": "Left"
    }
  ],
  "contexts": {
    "ProductSales": {
      "entity": "OrderItems",
      "projection": [],
      "filter": null,
      "sorts": null,
      "take": null,
      "skip": null,
      "relations": null
    }
  }
}
```

The graph rules are:

- relation references resolve by context key;
- context names are case-insensitive;
- a context may source another declared context;
- dependent contexts must be available before their consumers;
- recursive contexts must follow the engine's supported recursive shape;
- nested `contexts` inside a context definition are not part of this model.

## 7. Automatic grouping

There is no `groupBy` or `having` property in the model. The compiler derives
grouping from projection expressions:

- non-aggregate projections define the grouping grain;
- aggregate projections calculate values at that grain;
- aggregate filters are routed to the aggregate stage;
- `*Over` functions preserve detail rows instead of collapsing them.

Use a separate context when one aggregation must be completed before another.

## 8. Sort definitions

A sort definition contains a field expression and direction:

```json
{
  "field": {
    "kind": "field",
    "field": "TotalAmount"
  },
  "direction": "Descending"
}
```

`direction` is `Ascending` or `Descending`. Sort fields may reference source
fields, relation paths, or materialized aliases.

## 9. Serialization rules

- Property names use the canonical lower-camel-case JSON names shown here.
- `oper` is omitted for the default `Add` operation.
- Null optional properties may be omitted by the serializer, except where the
  configured JSON contract preserves an explicit null.
- Context names are dictionary keys, not `name` properties.
- Relations reference contexts by `refContext`; they do not embed definitions.
- Values remain typed JSON scalars and are parameterized by the execution
  layer.

Parser and model behavior is covered by the query-definition tests under
[`src/OQuery.UnitTest/Tests/Query/Definitions`](./src/OQuery.UnitTest/Tests/Query/Definitions).
