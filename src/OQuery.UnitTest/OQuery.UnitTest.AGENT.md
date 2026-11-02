# OQuery.UnitTest project agent instructions

## Scope

This project contains core-only NUnit tests for the OQuery engine across .NET
8, .NET 9, and .NET 10. Its query fixtures live under the repository
`data-storage/queries` directory so the core repository is independently
testable.

## Before changing code

- Read the production API and neighboring tests before adding assertions.
- Search all target-framework conditions and provider-specific tests.
- Keep core query fixtures under `data-storage/queries` and scripts under
  `data-storage/scripts`; do not reference `OQuery.UI`, `Demo.*`, or
  UI-repository paths.
- Treat Docker database tests as environment-dependent integration tests.

## Generating tests

- Use deterministic in-memory/SQLite setup where possible.
- Use clear NUnit test names and assert observable SQL, results, errors, or
  provider behavior.
- Preserve the existing fixture metadata and saved-query semantics when adding
  cases.
- Skip only when a required external service or native provider is genuinely
  unavailable, and include the reason in the skip message.
- Avoid secrets and machine-specific absolute paths.

## Validation

Run commands from the core repository root:

```powershell
dotnet test .\src\OQuery.UnitTest\OQuery.UnitTest.csproj
```

Use a test filter for focused changes and add `--framework net10.0` for a
single-framework run. Validate all target frameworks when changing shared
behavior. Docker-backed tests require the corresponding service from `docker/`.
