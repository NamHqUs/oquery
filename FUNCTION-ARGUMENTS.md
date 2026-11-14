# Function Arguments: Binding, Optional Arguments and Collection Arguments

This note explains how OQuery function definitions bind arguments, why the
`Required arguments cannot follow optional arguments` error appeared, and the
planned collection-argument design for fluent aggregates.

## 1. Argument model

A `SqlFunctionArgumentDefinition` has a `Name`, a `DataType`, and an optional
`DefaultValue`. An argument is **optional** when it has a default value
(`Optional => DefaultValue is not null`); otherwise it is **required**.

A function definition (`SqlFunctionDefintion`) validates its argument list in
its constructor, using one of two binding modes:

| Mode | Flag | Rule | Typical use |
|------|------|------|-------------|
| Left-to-right (default) | `bindArgumentsFromEnd: false` | Optional arguments must trail. A required argument after an optional one is rejected. | `Round(value, digits = 0)` |
| Right-to-left | `bindArgumentsFromEnd: true` | Optional arguments must lead. An optional argument after a required one is rejected. | `CountIf(expression = 1, predicate)` |

Supplied arguments are matched like this:

- **Left-to-right:** supplied arguments fill slots from the start. Missing trailing
  optional slots use their defaults.
- **Right-to-left:** supplied arguments fill slots from the end. Missing leading
  optional slots use their defaults. So `CountIf(IsAbnormal = <True>)` binds the
  predicate to the last slot and the default `1` to `expression`.

## 2. Why the error appeared

`CountIf` is defined with an optional leading `expression` and a required
`predicate`, and it must bind from the end. The definition is correct, but it
was rebuilt without its binding mode:

1. Each query calls `MetadataService.GetFunctionProvider`.
2. That calls `FunctionDefinitionService.GetAll()`, which converts every
   definition to `FunctionDefinitionDto` and back.
3. The DTO did not carry `BindArgumentsFromEnd`, so `CountIf` was rebuilt in
   left-to-right mode.
4. In left-to-right mode, the required `predicate` after the optional
   `expression` fails validation with
   `Required arguments cannot follow optional arguments.`

**Fix:** `FunctionDefinitionDto` now has a `BindArgumentsFromEnd` property. It is
written by `FromFunctionDefinition` and read by `ToFunctionDefinition`, so the
round-trip preserves the binding mode.

Any new property on `SqlFunctionDefintion` must be added to the DTO in both
directions, or the same class of error will return.

## 3. Fluent aggregates and the root context

Two forms exist:

- **Native aggregate on the root context:** `Sum(Salary)`, `CountIf(Status = <Paid>)`.
  No receiver is given, so the function applies to the root entity's rows and
  the engine groups as needed. The syntax is unchanged.
- **Fluent collection aggregate:** `Employees.Sum(Salary)`,
  `Employees.PerformanceReviews.AvgIf(Rating, ReviewYear = 2025)`. The path before
  the function is the receiver, and the engine generates a grouped join or
  correlated subquery for the collection.

## 4. Planned: collection argument type

Today the receiver of a fluent aggregate is not part of the function's
signature, so the editor cannot suggest aggregates by receiver type.

**Design (approved direction):**

- Add `DataType.Collection`. A bare enum value cannot say what kind of
  collection is meant, so each argument also carries a `CollectionKind`:
  - `Entity`: a table-backed entity (e.g. `Employees`).
  - `Relation`: a collection relation (e.g. `Employees.PerformanceReviews`).
  - `Context`: a named row-producing context (e.g. a `... is ...:` block).
- Every collection aggregate takes its receiver as the first, typed argument:
  `Sum(collection: Collection, expression: Number)`,
  `AvgIf(collection: Collection, expression: Number, predicate: Boolean)`.
  Receiver form is the only syntax the user writes (`Employees.Sum(Salary)`).
- Scope: all collection aggregates (Count, Sum, Avg, Min, Max, the `*If`
  variants, CountDistinct).
- Plain `Sum(x)` and `CountIf(...)` without a receiver remain native
  root-context aggregates and are not changed.
- Fluent suggestion after `<path>.` lists only functions whose first argument
  is a `Collection` whose `CollectionKind` matches the path's target.

**Open points to settle in the implementation:**

- `CountIf` has an optional leading argument and binds from the end. Adding a
  required collection receiver changes its signature, so the binding rule must
  be re-stated for the receiver form.
- Saved queries (`.oqr` / `.json`) that use `"function": "Employees.SumIf"` must
  keep parsing unchanged.
- User-defined function JSON needs the new `CollectionKind` field, with a default
  for existing files.
