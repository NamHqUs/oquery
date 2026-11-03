# Query Model JSON Reference

This document records the canonical JSON shape for backend projection
expressions. A projection expression is a flat `fields` array; there is no
root group and no `math` wrapper. This differs from the frontend TypeScript
expression type.

The backend serializer is configured by
[`JsonQueryDefinitionParser`](./src/OQuery/Contracts/QueryDefinitions/Parsers/JsonQueryDefinitionParser.cs),
which owns the projection and condition converters. Query model records are
plain POCOs and do not carry JSON or YAML parsing attributes.
The format is covered by
[`QueryDefinitionFormatParityTests`](./src/OQuery.UnitTest/Tests/Query/Definitions/QueryDefinitionFormatParityTests.cs).

## Query context graph

`Sorts` is nullable on both the main query and context definitions. JSON
`"sorts": null` means no ordering, just like an empty array. A non-null
`sorts` value is a flat array of sort definitions, not an array of arrays.

The query model stores context definitions in a dictionary. The dictionary key
is the context name; context definitions do not repeat that name property.
`relations` references contexts by name.

```json
{
  "entity": "Employees",
  "relations": [
    { "refContext": "C1" },
    { "refContext": "C2" }
  ],
  "contexts": {
    "C1": {
      "entity": "Departments",
      "projection": []
    },
    "C2": {
      "entity": "Employees",
      "relations": [
        {
          "refContext": "C1",
          "alias": "Department",
          "condition": "DepartmentId = C1.Id",
          "joinType": "Left"
        }
      ]
    }
  }
}
```

`refContext` is required. `alias` and `condition` are optional, and
`joinType` defaults to `Left`. Legacy array-based contexts and
`extraRelations`/`refContextId` input remain supported by the parser for
backward compatibility.

Canonical YAML uses the corresponding DTO names `Relations` and `RefContext`,
with dictionary-shaped `Contexts`. Null relation properties are omitted,
including `JoinType`: the YAML writer does not replace null with `Left`.
Canonical YAML with an omitted `JoinType` parses back to null; explicit join
types are preserved. Legacy `ExtraRelations`/`RefContextId` YAML remains
readable with its historical `Left` default.

JSON writes an explicit `"joinType": null` for null relation join types,
even when other null properties are omitted. This preserves null on save and
reload rather than applying the constructor's `Left` default for an omitted
JSON property.

## Nodes and operators

- Each field, value, function, or group node has an `oper` property in the
  model. JSON omits `oper` for the default `Add` operator and writes the
  operator symbol (for example, `"-"` or `"*"`) when it is not `Add`.
- A projection contains a `fields` array. The operator on each later item
  connects it to the preceding expression item.
- A group node has its own `oper` for how the group participates in its
  containing expression, and a `fields` array for the group's contents.
- Function arguments remain an array. An arithmetic argument is represented
  by a group; a single field or value is represented directly.
- Conditional aggregate arguments use a `condition` node. The nested
  condition uses the same filter model as query filters; field names remain
  field names and comparisons are represented by `operation` and `right`.
- Constants use `kind: "value"` and a scalar `value`.

## Flat expression

`A + B - <3>` is represented without a root group or math wrappers:

```json
{
  "fields": [
    { "kind": "field", "field": "A" },
    { "oper": "+", "kind": "field", "field": "B" },
    { "oper": "-", "kind": "value", "value": 3 }
  ],
  "alias": null
}
```

## Parenthesized expression

For `A * (B - C + D) / <2>`, the grouped term is a node in the projection
list. Its operation is multiplication; the operators inside its `fields`
array describe `B - C + D`:

```json
{
  "fields": [
    { "kind": "field", "field": "A" },
    {
      "oper": "*",
      "kind": "group",
      "fields": [
        { "kind": "field", "field": "B" },
        { "oper": "-", "kind": "field", "field": "C" },
        { "oper": "+", "kind": "field", "field": "D" }
      ]
    },
    { "oper": "/", "kind": "value", "value": 2 }
  ],
  "alias": null
}
```

The query compiler applies normal arithmetic precedence to flat terms and
retains parentheses through nested groups.

## Function arguments

`Round(A + B, <2>)` stores its arithmetic first argument as a group and the
constant second argument directly:

```json
{
  "fields": [
    {
      "kind": "function",
      "function": "Round",
      "arguments": [
        {
          "kind": "group",
          "fields": [
            { "kind": "field", "field": "A" },
            { "kind": "field", "field": "B" }
          ]
        },
        { "kind": "value", "value": 2 }
      ]
    }
  ],
  "alias": null
}
```

For `5 - (A - B) * (C - D)`, the two parenthesized operands are sibling
groups, each carrying its operator in the containing list:

```json
{
  "fields": [
    { "kind": "value", "value": 5 },
    {
      "oper": "-",
      "kind": "group",
      "fields": [
        { "kind": "field", "field": "A" },
        { "oper": "-", "kind": "field", "field": "B" }
      ]
    },
    {
      "oper": "*",
      "kind": "group",
      "fields": [
        { "kind": "field", "field": "C" },
        { "oper": "-", "kind": "field", "field": "D" }
      ]
    }
  ],
  "alias": null
}
```

Conditional aggregates use a structured condition rather than embedding a
predicate in a field name. This keeps `IsAbnormal` a normal field while
allowing the SQL compiler to render the comparison for each dialect:

```json
{
  "fields": [
    {
      "kind": "function",
      "function": "CountIf",
      "arguments": [
        {
          "kind": "condition",
          "condition": {
            "logicalOperation": 0,
            "filters": [
              {
                "logicalOperator": 0,
                "left": { "kind": "field", "field": "IsAbnormal" },
                "operation": 0,
                "right": { "kind": "value", "value": true },
                "parameterized": true
              }
            ]
          }
        }
      ]
    }
  ],
  "alias": "AbnormalCount"
}
```

The same shape supports string, numeric, compound, and range predicates.
`CountIf` and `AvgIf` compile the condition as a conditional aggregate, so
they do not require a separate filtered context and join.

The reader continues to accept legacy projection objects that contain a
singular `field` and legacy `math` nodes. Serialization always writes the
canonical `fields` array and stores operators directly on expression nodes.

## Automatic aggregate grouping

`QueryDefinition` has no `GroupBy` or `Having` properties. Do not add them when
creating a query definition. The compiler detects aggregate functions such as
`COUNT`, `SUM`, and `AVG` in projections and automatically groups by the
projected expressions that do not contain aggregates. For example, projecting
`Category`, `Channel`, and `SUM(Revenue)` groups the result by `Category` and
`Channel`. Filters remain row-level filters; they are not a `HAVING` clause.

Use contexts (CTEs) when one aggregation must be performed before another, such
as calculating per-order category revenue in a context and then averaging those
per-order values in the outer query. Context query projections use the same
automatic grouping rule.

## QueryDefinition contexts and CTEs

The top-level JSON accepted by
[`QueryDefinitionParser.Parse`](./src/OQuery/Contracts/QueryDefinitions/Parsers/QueryDefinitionParser.cs)
can contain a `contexts` array. A context defines a CTE query and optionally
the join condition that connects it to the outer source. `contexts` is query
structure, not a projection-expression node; expressions inside its query and
condition use the same canonical field/value/group/function node shapes above.

```json
{
  "entity": "User",
  "projection": [
    {
      "fields": [{ "kind": "field", "field": "Id" }],
      "alias": null
    },
    {
      "fields": [{ "kind": "field", "field": "Manager.Alias Name" }],
      "alias": "Manager Name"
    }
  ],
  "contexts": [
    {
      "name": "UserContext",
      "alias": "Manager",
      "joinType": "Left",
      "query": {
        "entity": "User",
        "projection": [
          {
            "fields": [{ "kind": "field", "field": "Id" }],
            "alias": null
          },
          {
            "fields": [{ "kind": "field", "field": "Name" }],
            "alias": "Alias Name"
          }
        ]
      },
      "condition": {
        "logicalOperator": null,
        "left": { "kind": "field", "field": "ManagerId" },
        "operation": 0,
        "right": { "kind": "field", "field": "Id" },
        "parameterized": false
      }
    }
  ]
}
```

Context properties:

- `name` is the CTE identifier. Relations with the same name share one CTE;
  they must use the same query definition.
- `alias` is an optional relation alias used to address the joined context in
  projection and condition field paths. If omitted, `name` is used.
- `query` is another query definition, using the same `entity`, `projection`,
  filter, sort, and paging properties.
- `joinType` accepts `Inner`, `Left`, `Right`, or `Full`; it defaults to
  `Left`. Omit `condition` to declare a CTE without joining it.
- `recursive` is optional and defaults to `false`. Set `"recursive": true` in
  JSON or `Recursive: true` in YAML when the recursive member references its
  CTE only through the join `condition`. The compiler validates that a
  recursive context actually references itself.
- `condition` uses the condition model. `operation` is the numeric
  `BinaryOperator` value (`0` is `Equals`); a string operation is interpreted
  as a custom operator ID. A range condition uses operation `8` (`Between`)
  and a `kind: "group"` right operand containing its two bounds.

For join conditions, unqualified left-side fields resolve against the outer
source and unqualified right-side fields resolve against the joined context.
Prefix field paths with a CTE name when another source is intended. The outer
`entity` may itself name a CTE, enabling a query to use that CTE as its
`FROM` source. Recursive CTEs and contexts nested inside another context
query are not supported. A context that references itself through a recursive
projection is emitted as a recursive CTE when the selected dialect supports
recursive CTEs. For a self-reference that appears only in the join condition,
set `recursive` to `true` in JSON or `Recursive: true` in YAML.
