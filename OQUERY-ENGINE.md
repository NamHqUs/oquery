# OQuery engine rules

This document is the technical source of truth for OQuery execution. It
defines the invariants that the parser, compiler, SQL generator, metadata
provider, and execution layer must preserve. If implementation behavior
conflicts with this document, treat the behavior as an engine defect and add a
regression test before changing the rule.

Authoring syntax belongs in [OQUERY-SYNTAX.md](./OQUERY-SYNTAX.md). This
document intentionally does not repeat syntax examples.

All semantic rules have one owner: this document. The other OQuery documents
define their own representation and must refer here for execution meaning.

## 1. Execution pipeline

The engine processes a query through these stages:

1. `QueryDefinitionOQueryParser` converts OQuery text into a
   `QueryDefinition`.
2. The parser identifies the main context and its source entity.
3. `DbQueryFromQueryDto` chooses the direct query path or the context query
   path.
4. Visitors build `TableExpression` and `SqlExpression` objects.
5. `SqlStatementGenerator` materializes projection dependencies and renders
   SQL.
6. `DbQueryProvider` executes the generated statement and returns results.

The frontend, API controller, and query service must not reimplement these
rules. They may validate input and report errors, but query semantics belong
to the core engine.

## 2. Main execution entry point

An unqualified header uses the source entity name as both the context name and
the main execution entry point. An explicit context name replaces that
default. The parser stores the context name separately from the source entity
so the distinction survives compilation.

`QueryDefinitionOQueryParser.ReadBlocks` parses the header and applies the
implicit-name rule. `QueryDefinitionOQueryParser.Parse` chooses the root block:
the block that is not referenced by another context. A single block is
therefore always the main entry point.

For multiple blocks, the root must be unambiguous. Context references are
validated by `ValidateContextReferences`, and an unknown referenced context is
an error.

## 3. Context dependency rules

Each named context has:

- a context name;
- a source entity or source context;
- projections;
- optional filters, sorts, paging, and joins.

Context dependencies are directional. A context may depend only on contexts
that are available in its compilation scope. The compiler must reject:

- unknown context references;
- context dependency cycles;
- duplicate context aliases;
- forward context declarations when the query form requires declaration order;
- unsupported recursive-context shapes.

`QueryDefinitionVisitor` is responsible for binding context names, validating
context aliases and declaration order, creating CTE table expressions, and
building context joins.

When a user executes one context from a complete OQuery document, the engine
must compile only the selected context and its transitive named dependencies.
Unrelated blocks are not part of that execution graph and must not prevent the
selected context from running. A malformed selected block or a malformed
dependency remains an execution error.

## 4. Projection alias scope

All projection aliases declared in one context belong to one symbol scope.
Their availability does not depend on their position in the requested output
columns.

Compilation must therefore:

1. collect every projection alias before resolving projection expressions;
2. distinguish implicit field output names from explicit aliases;
3. build an alias dependency graph;
4. resolve dependencies independently of output order;
5. reject alias dependency cycles;
6. preserve the original projection order in the result.

An alias that has the same text as an unaliased source field path is not a
forward alias declaration. It must continue to resolve as the source field.

`SelectVisitor` owns this behavior for the direct query path. `QueryContext`
resolves an alias before falling back to metadata field lookup. The alias
scope must also be available when a function argument references an alias
declared later in the projection list.

An empty projection is a select-all request for the current source. A
projection wildcard inherits the complete projection of the source context,
not only the source entity metadata. Explicit projection items after the
wildcard are appended to the inherited fields.

This rule is authoritative for OQuery text, `QueryDefinition` JSON, and
compiler behavior. Do not copy or redefine it in another document.

## 5. Projection dependency layers

The SQL generator must materialize projection aliases in dependency layers.
The layer is determined by the dependency graph, not by the order of the
requested columns.

Required invariants:

- source-only projections are in the base layer;
- an alias depending on a base projection is in the next layer;
- aliases with the same dependency depth share one layer;
- independent aliases must not create unnecessary nested wrappers;
- output column order remains the order requested by the user;
- filters and sorts that reference an alias are applied only after that alias
  has been materialized;
- source-only predicates remain eligible for pushdown before grouping or
  window evaluation.

`SqlStatementGenerator.TryRenderProjectionLayers` calculates dependency
levels, rejects alias cycles, separates source predicates from alias
predicates, and creates the minimum required derived-query layers.

## 6. Aggregates and window functions

Grouping aggregates and `*Over` functions have different semantics:

- grouping aggregates can collapse rows and may create `GROUP BY`;
- `*Over` functions preserve the current row set and render as window
  expressions;
- aggregate predicates on the current grouped query belong in `HAVING`;
- aggregate expressions over a context-qualified field in a filter may require
  a scalar subquery over that context;
- an ordinary aggregate must not be used where a repeated scalar value is
  required.

`QueryContext` parses function arguments and distinguishes ordinary aggregate
processing from context aggregate processing. `SqlExpressionExtensions`
provides aggregate and projection-alias inspection used by SQL layer
generation.

## 7. Metadata and authorization boundary

Logical entity and field names are resolved through the metadata provider.
Physical schema qualification belongs only to generated SQL.

`QueryContext.ParseFieldPath` must resolve:

1. an already-known projection alias;
2. a pending alias in the current projection scope;
3. a joined context path;
4. a metadata field or relation path.

If metadata lookup fails, the engine must report the missing logical field
instead of silently treating it as a raw SQL expression.

Authorization must be enforced using the metadata and query information at
the final SQL generation trust boundary. Adding a new projection, function,
context, or join must not create a path that bypasses that boundary.

## 8. Error handling invariants

The engine must fail explicitly for invalid query semantics. It must not:

- fall back to a physical field when a declared alias is unresolved;
- silently drop a projection or predicate;
- generate success-shaped results after compilation failure;
- hide metadata, context, alias-cycle, or authorization errors;
- convert an invalid input into an empty query.

Errors should identify the logical entity, field, context, alias, or function
that caused the failure. API layers may translate the exception into an HTTP
response, but must preserve the diagnostic cause in logs.

## 9. Regression-test requirements

Every engine rule in this document requires a focused test at the narrowest
layer that owns the behavior.

At minimum, tests must cover:

- implicit and explicit main entry points;
- same-context aliases referenced before and after their output position;
- multiple aliases at the same dependency depth;
- alias cycles;
- aliases in filters and sorts;
- context dependency order and unknown contexts;
- grouping versus window-function semantics;
- metadata field-not-found and authorization failures.

Existing tests should be extended rather than duplicated when they already
cover the same semantic path.

## 10. Source-of-truth map

| Responsibility | Primary class |
|---|---|
| OQuery header and block parsing | `QueryDefinitionOQueryParser` |
| DTO/text query construction | `DbQueryFromQueryDto` |
| Direct projection compilation | `SelectVisitor` |
| Context binding and CTE construction | `QueryDefinitionVisitor` |
| Field, relation, function, and alias resolution | `QueryContext` |
| SQL dependency layers and SQL rendering | `SqlStatementGenerator` |
| Metadata field and entity resolution | `MetadataProvider` and `Entity` |
| Query execution | `DbQueryProvider` |
