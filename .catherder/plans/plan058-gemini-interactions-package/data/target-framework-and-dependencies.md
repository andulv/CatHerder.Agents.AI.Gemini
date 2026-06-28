# Target Framework And Dependencies

**Decision timestamp:** 2026-05-06T17:29:58+02:00

## Target Framework

Use `net8.0` for the package and test projects.

Rationale:

- The development machine has .NET 8, 9, and 10 SDKs installed.
- `net8.0` is the lowest practical LTS target available locally and is more suitable for NuGet consumers than `net10.0`.
- CatHerder can reference a `net8.0` library from its `net10.0` projects.

## Dependency Versions

Initial package dependencies:

- `Microsoft.Agents.AI` version `1.4.0`
- `Microsoft.Extensions.Logging.Abstractions` version `10.0.7`

Initial test dependencies:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- Integration-test configuration packages resolved to `Microsoft.Extensions.Configuration.*` version `10.0.7`

## Dependency Policy

- Keep the reusable package independent of `CatHerder.*` projects and namespaces.
- Do not depend on `Google_GenerativeAI.Microsoft` for the first extraction; it targets a different API layer.
- Use `dotnet add package` / `dotnet remove package` for package references.
