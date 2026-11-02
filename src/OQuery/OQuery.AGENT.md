# OQuery project agent instructions

## Scope

This is the core OQuery library: metadata modeling, query definitions,
expression compilation, SQL dialects, execution, parsing, and result
materialization. It must remain independent of UI and demo projects.

## Before changing code

- Search symbols, call sites, interfaces, dialect implementations, and tests
  before adding or changing core behavior.
- Trace the full path from public API to query model, compiler, dialect, and
  execution before editing a hot path.
- Before generating code, evaluate the algorithm, performance characteristics,
  and architecture; choose the design before implementing details.
- Check all supported providers and target frameworks (.NET 8, 9, and 10).
- Preserve public API compatibility unless the task explicitly requires a
  breaking change.

## Generating code

- Keep provider-specific SQL in dialect abstractions and preserve parameterized
  SQL; never concatenate untrusted values into commands.
- Preserve metadata-driven and runtime-schema behavior; do not introduce demo
  entities or UI services into core.
- Prefer existing model types, parsers, visitors, and helpers over duplicates.
- Surface invalid query/model states with the established typed exceptions.
- Keep allocations and async behavior appropriate for query execution paths.
- Update focused unit tests and relevant query-model documentation for behavior
  changes.

## Validation

Run commands from the core repository root:

```powershell
dotnet build .\src\OQuery.slnx
dotnet test .\src\OQuery.UnitTest\OQuery.UnitTest.csproj
```

Use the smallest relevant test filter for focused changes. Validate all target
frameworks when changing shared core behavior.
