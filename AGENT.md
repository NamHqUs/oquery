# OQuery repository instructions

## Scope

This repository contains the core OQuery engine and its NUnit test project.
Keep the core project independent from `OQuery.UI` and the demo projects.

## Repository layout

- Production code is under `src/OQuery`.
- Unit tests are under `src/OQuery.UnitTest`.
- Query-definition fixtures are under `data-storage/queries`.
- Database and report scripts are under `data-storage/scripts`.

Preserve the `oquery` submodule boundary: core code must not reference UI or
demo projects.

## Design-first implementation

Before generating code, consider the algorithm, performance characteristics,
and architecture. Prefer a design that addresses the full execution path,
avoids unnecessary repeated work, and keeps responsibilities at the correct
layer.

## Validation

Run commands from this repository root:

```powershell
dotnet build .\src\OQuery.slnx
dotnet test .\src\OQuery.UnitTest\OQuery.UnitTest.csproj
```

Database-provider tests may require the corresponding Docker service.
