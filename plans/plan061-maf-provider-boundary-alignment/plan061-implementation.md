---
type: plan-implementation
description: "Plan 061 - Implement MAF / MEAI provider boundary alignment"
status: active
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:28:23+02:00
---
# Plan 061 Implementation — MAF / MEAI Provider Boundary Alignment

## 0. Required Context

- Spec: `plan061-spec.md`
- `plan-task-standards`
- `.agents/instructions/project.instructions.md`
- `README.md`
- `Directory.Build.props`
- Parent `catherder-dev` root `Directory.Packages.props` when developing inside
  the full repository with central package management
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiSseEventReducer.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiUsageMapper.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiBuiltInToolBridge.cs`
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/`

## 1. Tasks

Allowed task statuses: not-started, in-progress, blocked, implemented, reviewed, completed.

| Status | Task |
|---|---|
| `implemented` | [Task P061-T01: Add Gemini protocol exception contract](tasks/task061-01-protocol-exception-contract.md) |
| `implemented` | [Task P061-T02: Make non-streaming response parsing fail fast](tasks/task061-02-nonstreaming-fail-fast.md) |
| `implemented` | [Task P061-T03: Make SSE parsing and reducer state fail fast](tasks/task061-03-sse-fail-fast.md) |
| `implemented` | [Task P061-T04: Align built-in tool content and telemetry behavior](tasks/task061-04-built-in-tool-telemetry.md) |
| `implemented` | [Task P061-T05: Align caller options and streaming fallback behavior](tasks/task061-05-options-and-fallback.md) |
| `implemented` | [Task P061-T06: Align provider error and finish semantics](tasks/task061-06-error-finish-semantics.md) |
| `implemented` | [Task P061-T07: Verify package boundary and cleanup stale tests](tasks/task061-07-boundary-verification.md) |

## 2. Task Parallelism

T01 should run first because later tasks should throw the public
`GeminiProtocolException` boundary contract.

T02 and T03 may run in parallel after T01 if they do not edit the same helper
methods. If both need shared parsing helpers, sequence them and keep helper
changes in the first task that touches them.

T04 may run after T01. It can run in parallel with T02/T03 only if the executor
coordinates edits to `GeminiBuiltInToolBridge` and streaming finalization.

T05 and T06 should run after T02/T03 so they can rely on the final parser and
streaming behavior.

T07 runs last. It is the consolidation and verification task.

## 3. Acceptance Criteria

- [x] The Gemini package exposes a public `GeminiProtocolException` for
  successful-response / established-stream provider protocol violations.
- [x] Non-streaming current-schema parsing fails visibly for malformed required
  data and preserves valid current-schema behavior.
- [x] Streaming SSE parsing fails visibly for malformed known current-schema
  data and preserves valid current-schema update reconstruction.
- [x] Unknown noncritical additive SSE events are tolerated only with explicit
  debug logging and no state mutation.
- [x] Provider-side built-in tool observations remain standard informational
  MEAI content and no longer emit default Gemini-owned OpenTelemetry spans with
  ungated raw arguments/results.
- [x] Unsupported caller options and default streaming fallback behavior match
  the spec decisions.
- [x] Provider errors, refusals, and finish reasons follow the audited
  Microsoft.Extensions.AI OpenAI Chat / Responses boundary behavior.
- [x] Legacy `outputs` response support and legacy GenerateContent usage aliases
  are not reintroduced.
- [x] Package-local unit tests cover valid current-schema behavior and the
  planned fail-fast cases.
- [x] `dotnet test CatHerder.Agents.AI.Gemini.slnx` passes, except live tests
  that are explicitly credential-gated and skipped by default.
