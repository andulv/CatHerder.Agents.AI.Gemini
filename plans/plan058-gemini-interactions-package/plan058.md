---
type: plan
description: "Plan 058 - Extract Gemini Interactions MAF client into a publishable package/workspace"
status: active
---

# Plan 058: Gemini Interactions Package Extraction

**Status:** active
**Created:** 2026-05-06T15:33:54+02:00
**Updated:** 2026-05-06T17:42:54+02:00

## Goal

Move the Gemini Interactions Microsoft Agent Framework client out of `CatHerder.Agents.Core` into its own standalone .NET workspace, with a publishable NuGet package, one ordinary offline unit test project, and one gated integration test project that exercises the real Gemini Interactions API.

## Context / Why

CatHerder now contains a working raw-HTTP `IChatClient` implementation for the Gemini Interactions API under `src/CatHerder.Agents.Core/AgentProviders/Gemini/`. The reusable parts are the chat client, options, request DTOs, API exception, SSE reducer, and Gemini built-in tool bridge. `GeminiAgentProvider` is CatHerder-specific runtime glue and should stay in CatHerder.

The new package should feel natural to users familiar with Microsoft Agent Framework provider packages. The local Microsoft reference source shows these patterns:

- `Microsoft.Agents.AI.OpenAI` has a focused package project, `Extensions/` methods such as `AsAIAgent`, `ChatClient/` internals for streaming helpers, and a separate `Microsoft.Agents.AI.OpenAI.UnitTests` project.
- OpenAI and Anthropic live API scenarios are split into integration test projects such as `OpenAIResponse.IntegrationTests` and `AnthropicChatCompletion.IntegrationTests`, with fixtures and conformance-style `ChatClientAgent` runs.
- Package projects carry NuGet metadata (`Title`, `Description`) and import shared packaging props in the Microsoft repo. The new workspace needs its own equivalent packaging setup.

`Google_GenerativeAI.Microsoft` should be treated as secondary reference material only. Its NuGet page describes a `Microsoft.Extensions.AI` `IChatClient` wrapper over the unofficial `Google_GenerativeAI` SDK with streaming and DI support, but it is not a Microsoft Agent Framework provider package and does not appear to target the Gemini Interactions API that this client implements.

## Scope

In scope:

- Create a new standalone .NET workspace/repository layout for the package.
- Port the reusable Gemini Interactions client code out of CatHerder namespace and dependencies.
- Add Microsoft-style extension methods and `ChatClientAgent` convenience APIs.
- Add one offline unit test project.
- Add one real-API integration test project that skips unless credentials/configuration are present.
- Add README, package metadata, pack validation, and basic GitHub/NuGet publish preparation.
- Update CatHerder to consume the new package/project once the package workspace exists.

Out of scope for the first extraction:

- Publishing to NuGet before package name, owner, license, and versioning policy are approved.
- Moving CatHerder model catalog/configuration code into the package.
- Implementing Vertex AI authentication unless chosen during the package API design task.
- Rewriting the client to use the old `Google_GenerativeAI.Microsoft` package.

## Proposed Workspace Shape

Use this as the starting shape, with names finalized in T01:

```text
<repo-root>/
  <solution>.slnx
  Directory.Build.props
  README.md
  LICENSE
  src/
    <PackageId>/
      <PackageId>.csproj
      GeminiInteractionsChatClient.cs
      GeminiInteractionsChatClientOptions.cs
      GeminiApiException.cs
      Extensions/
        GeminiInteractionsChatClientExtensions.cs
      Internal/
        GeminiInteractionsRequestModels.cs
        GeminiBuiltInToolBridge.cs
        GeminiBuiltInToolTelemetry.cs
        GeminiSseEventReducer.cs
  tests/
    <PackageId>.UnitTests/
      <PackageId>.UnitTests.csproj
    <PackageId>.IntegrationTests/
      <PackageId>.IntegrationTests.csproj
```

The package must not use `Microsoft.*` as its package ID or namespace. A placeholder such as `<ChosenRoot>.Agents.AI.Gemini` is used until the naming decision is made.

## Tasks

### Phase 1 - Product Boundary And Reference Alignment

- [x] **T01** Decide package identity: repository name, NuGet package ID, root namespace, title, description, license, tags, and preview/stable version policy. Record the decision in `data/package-identity.md`.
- [x] **T02** Produce `data/reference-layout-notes.md` comparing the package shape to `Microsoft.Agents.AI.OpenAI` and `Microsoft.Agents.AI.Anthropic`: source folders, extension method names, package metadata, unit tests, integration fixtures, and conformance-style scenarios.
- [x] **T03** Produce `data/public-api-sketch.md` with constructor signatures, API-key/`HttpClient` creation helpers, `AsAIAgent` extension methods, optional DI helpers, options shape, and intentionally unsupported features.
- [x] **T04** Decide minimum target framework and dependency versions. Prefer the lowest practical supported framework for current MAF packages, and record why if `net10.0` remains necessary.

### Phase 2 - New Workspace Skeleton

- [x] **T05** Create the standalone workspace/repository skeleton with solution, `Directory.Build.props`, package project, unit test project, integration test project, README, license placeholder, and standard `.gitignore`/editor configuration.
- [x] **T06** Configure package metadata and pack settings: `PackageId`, `Title`, `Description`, `PackageTags`, `RepositoryUrl`, `PackageReadmeFile`, license, symbols/source-link if chosen, deterministic build settings, and `dotnet pack` output.
- [x] **T07** Add project references and package references using `dotnet` CLI commands where applicable; avoid manual package drift. Keep dependency surface limited to Microsoft Agent Framework/Microsoft.Extensions.AI plus BCL HTTP/JSON unless a specific SDK dependency is chosen.

### Phase 3 - Port And De-CatHerder The Client

- [x] **T08** Move the reusable files from `CatHerder.Agents.Core/AgentProviders/Gemini` into the new package namespace: `GeminiInteractionsChatClient`, options, request models, `GeminiApiException`, `GeminiSseEventReducer`, and built-in tool bridge.
- [x] **T09** Remove CatHerder-specific dependencies and names. Replace `CatHerderTelemetry.AgentActivitySource` with package-owned telemetry or remove telemetry from the reusable core, and remove all `CatHerder.*` namespace references.
- [x] **T10** Split internals into Microsoft-familiar folders (`Extensions/`, `Internal/`, optionally `ChatClient/`) without over-abstracting the current working implementation.
- [x] **T11** Add extension methods that mirror Microsoft Agent Framework provider ergonomics: create `IChatClient` from the Gemini Interactions client/factory and create `ChatClientAgent` via `AsAIAgent` overloads accepting instructions, name, description, tools, client factory, logger factory, and services.
- [x] **T12** Add XML documentation and README examples for non-streaming, streaming, tool calls/results, built-in tools, and `ChatClientAgent` usage.

### Phase 4 - Offline Unit Tests

- [x] **T13** Migrate the existing fake-HTTP Gemini tests from `CatHerder.Agents.Core.Tests` into `<PackageId>.UnitTests`, preserving coverage for request payloads, tool definitions, function-call/result correlation, response mapping, errors, and usage mapping.
- [x] **T14** Add unit tests for the new public extension methods and constructor/factory behavior, including argument validation and default options.
- [x] **T15** Add focused SSE reducer/client tests for text streams, function call streams, built-in tool streams, unknown events, malformed frames, fallback behavior, and cancellation.
- [x] **T16** Add pack/build smoke tests or scripted validation so unit test runs prove the package can build and pack without network access.

### Phase 5 - Real API Integration Tests

- [x] **T17** Create `<PackageId>.IntegrationTests` with an explicit live-test fixture that reads configuration from environment variables/user secrets, then skips cleanly when credentials are absent. Candidate variables: `GOOGLE_API_KEY`, `GEMINI_INTERACTIONS_MODEL`, optional `GEMINI_INTERACTIONS_BASE_URL`, and optional flags for built-in tools.
- [x] **T18** Add live non-streaming tests: simple text response, `ChatClientAgent` basic run, and invalid-key/error-shape handling if this can be done safely without leaking secrets.
- [x] **T19** Add live streaming tests: incremental text response and final usage/metadata when available.
- [x] **T20** Add live tool tests: local function call round trip through `ChatClientAgent`; optional built-in Google tool smoke test gated by a separate opt-in flag because availability, cost, and output shape may vary.
- [x] **T21** Document integration test configuration, cost/network expectations, skip behavior, and recommended local command lines.

### Phase 6 - CatHerder Consumption

- [x] **T22** Replace CatHerder's embedded reusable Gemini client code with a project/package reference to the new package. Keep `GeminiAgentProvider`, model catalog, configuration, and CatHerder-specific telemetry/configuration in CatHerder.
- [x] **T23** Move or delete obsolete Gemini client tests from `CatHerder.Agents.Core.Tests` after their package equivalents exist, leaving only CatHerder provider integration/configuration tests where useful.
- [x] **T24** Build CatHerder and run relevant tests to confirm the provider still creates a working `IChatClient` and the Web/runtime path still compiles.

### Phase 7 - Publish Readiness

- [ ] **T25** Add GitHub Actions or equivalent CI for restore, build, unit tests, pack, and optional manual integration-test workflow.
- [ ] **T26** Verify NuGet package contents with `dotnet pack` and local package install into a scratch/sample project.
- [ ] **T27** Finalize README publish badges/instructions, changelog/release notes, version number, and NuGet publish checklist.
- [ ] **T28** Record any known limitations: Interactions API preview status, Vertex AI support, built-in tool variability, streaming event assumptions, and MAF version compatibility.

## Acceptance Criteria

- [x] A standalone workspace exists with one package project, one offline unit test project, and one live integration test project.
- [x] The package builds and packs without depending on any `CatHerder.*` project or namespace.
- [x] The public API has documented constructors/factories and `AsAIAgent` convenience methods that feel familiar beside Microsoft Agent Framework OpenAI/Anthropic provider packages.
- [x] Unit tests run without network access and cover the currently embedded Gemini client behavior.
- [x] Integration tests are credential-gated, skip cleanly by default, and exercise real Gemini Interactions non-streaming and streaming calls when configured.
- [x] CatHerder consumes the extracted package/project and no longer carries duplicate reusable Gemini client source.
- [x] README and package metadata are sufficient for a GitHub/NuGet preview release.
- [x] `dotnet build`, `dotnet test` for unit tests, and `dotnet pack` succeed in the new workspace.
- [x] Relevant CatHerder build/tests succeed after switching to the extracted package.

## Notes

- Branch creation is intentionally deferred until execution starts, per CatHerder git rules.
- The new repository/workspace path is not chosen yet. During execution, confirm the target path before creating files outside `catherder-dev`.
- Current reusable source candidates in CatHerder are under `src/CatHerder.Agents.Core/AgentProviders/Gemini/`; `GeminiAgentProvider` is not part of the package boundary.
- Microsoft reference source is local at `/home/anders/source/agent/agent-framework/dotnet/src/Microsoft.Agents.AI.OpenAI` and `/home/anders/source/agent/agent-framework/dotnet/src/Microsoft.Agents.AI.Anthropic`.
- Current Microsoft examples emphasize the single `ChatClientAgent` abstraction over provider-specific clients; the new package should make that path obvious.
- Existing CatHerder build validation may need `-p:NuGetAudit=false` if unrelated NuGet audit warnings block feature validation.
- 2026-05-06T17:41:55+02:00: Execution intentionally stopped after T24 per user request. Phase 7 publish-readiness tasks remain open.