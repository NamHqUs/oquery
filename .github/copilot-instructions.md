# OQuery core repository instructions

Before searching or editing core code, read the nearest project agent file:

- `src/OQuery/**` ->
  [`src/OQuery/OQuery.AGENT.md`](../src/OQuery/OQuery.AGENT.md)
- `src/OQuery.UnitTest/**` ->
  [`src/OQuery.UnitTest/OQuery.UnitTest.AGENT.md`](../src/OQuery.UnitTest/OQuery.UnitTest.AGENT.md)

Paths and validation commands are relative to this core repository root.
For changes spanning both core projects, read both mapped files before editing.

The core repository must contain only the OQuery library and its core unit
tests. Never add references from core code or tests to application or demo
projects, external workspace paths, or machine-specific paths. Use the project
agent file for search order, code-generation boundaries, and validation
commands.
