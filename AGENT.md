# OQuery repository instructions

## Scope

This repository contains the core OQuery engine and its NUnit test project.
Keep the core project independent from `OQuery.UI` and the demo projects.

## Repository layout

- Production code is under `src/OQuery`.
- Unit tests are under `src/OQuery.UnitTest`.
- Query-definition fixtures are under `data-storage/queries`.
- Database and report scripts are under `scripts`.

Preserve the `oquery` submodule boundary: core code must not reference UI or
demo projects.

## Validation

Run commands from this repository root:

```powershell
dotnet build .\src\OQuery.slnx
dotnet test .\src\OQuery.UnitTest\OQuery.UnitTest.csproj
```

Database-provider tests may require the corresponding Docker service.
