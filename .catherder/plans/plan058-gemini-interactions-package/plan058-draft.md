---
type: plan
description: "Plan 058 draft - Extract Gemini Interactions MAF client into a publishable package/workspace"
status: draft
---

# Plan 058 Draft: Gemini Interactions Package Extraction

**Status:** draft
**Created:** 2026-05-06T15:33:54+02:00
**Updated:** 2026-05-06T15:33:54+02:00

## Goal

Extract the current `GeminiInteractionsChatClient` implementation from `CatHerder.Agents.Core` into a standalone, publishable .NET workspace and NuGet package for the Gemini Interactions API, with one offline unit test project and one gated live API integration test project.

## Context / Why

The current Gemini Interactions client is useful beyond CatHerder, but it is embedded in `CatHerder.Agents.Core` and carries CatHerder-specific namespace and telemetry dependencies. Publishing it separately requires a clean package boundary, public API naming, package metadata, documentation, test separation, and a consumption path back into CatHerder.

The Microsoft Agent Framework OpenAI and Anthropic packages are the primary reference implementations. The new project should feel familiar to developers who know `Microsoft.Agents.AI.OpenAI` and `Microsoft.Agents.AI.Anthropic`, especially in project layout, extension-method ergonomics, `ChatClientAgent` integration, and test organization. This is a familiarity goal, not a license to copy Microsoft source.

`Google_GenerativeAI.Microsoft` is useful as a secondary reference for Microsoft.Extensions.AI adapter ergonomics and README framing, but it targets the older/unofficial Google Generative AI SDK surface rather than the Gemini Interactions API and Microsoft Agent Framework package pattern. It should not be the core dependency or architecture reference unless research disproves that assumption.

## What We Want To Achieve (Outcomes)

- A standalone repository/workspace can build, test, pack, and later publish a Gemini Interactions Microsoft Agent Framework client.
- The package has a clean public surface with no dependency on `CatHerder.*` types.
- Offline unit tests cover request mapping, response mapping, error handling, tool calls/results, built-in tool content, and SSE reduction using fake HTTP handlers.
- Live integration tests run only when explicitly configured with real Google API credentials and model names.
- CatHerder can switch from embedded source to consuming the package while retaining its provider-specific configuration and model catalog glue.
- Repository docs explain quick start, API-key setup, streaming, tool calls, built-in Gemini tools, integration test configuration, and package status.

## Summary Of Work Needed

Create the new workspace skeleton, port the client and helpers, remove CatHerder-specific dependencies, add Microsoft-style extension methods and package metadata, migrate/expand tests into unit and integration projects, wire CI/packaging, update CatHerder to consume the package, and document publish readiness.

## Key Principles / Constraints

- Keep this phase in planning mode only; execution must happen on a dedicated branch when started.
- Treat files outside `catherder-dev` as read-only until execution explicitly targets a new repository/workspace path.
- Do not use `Microsoft.*` as the package ID or root namespace for an independently published package.
- Mimic Microsoft Agent Framework package patterns where they improve familiarity: project layout, extension-method shape, `AsAIAgent` convenience APIs, unit/integration test split, and conformance-style scenarios.
- Avoid CatHerder runtime dependencies in the package; CatHerder-specific provider registration remains in CatHerder.
- Prefer the raw Interactions API implementation unless a current official Google SDK supports the same Interactions API semantics cleanly.
- Integration tests must be opt-in, credential-gated, and safe to skip in normal CI.
- Unit tests must be deterministic and network-free.

## Open Questions

- What should the public GitHub repository, NuGet package ID, and root namespace be? Working placeholder: `<ChosenRoot>.Agents.AI.Gemini`.
- Which minimum target framework should the package support? Candidate default: `net8.0` if current Microsoft Agent Framework dependencies allow it; avoid `net10.0` unless necessary.
- Should the package expose only `HttpClient`/API-key constructors, or also DI registration helpers?
- Should Gemini built-in tool telemetry be package-owned via an internal `ActivitySource`, optional, or removed from the reusable package?
- Which license, icon, repository URL, package tags, and publish owner should be used for NuGet?
- Should the first version be explicitly marked preview until Gemini Interactions API shape and MAF dependency versions stabilize?