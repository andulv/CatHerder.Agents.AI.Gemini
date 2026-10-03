---
type: plan-implementation
description: "Plan 065 - keep the call-ID-to-name map small: record only response/stream calls, prune after the carrying request completed, document the live-API name requirement"
status: active
created: 2026-10-03T18:50:52+02:00
updated: 2026-10-03T19:20:10+02:00
---
# Plan 065 Implementation — Function-Name Map Pruning

## 0. Required Context

- Spec: `plan065-spec.md`
- Investigation data: `data/function-name-map-investigation.md`
- `plan-task-standards` skill
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
  (`FunctionNamesByCallIdStateKey` `:21`, `RememberFunctionName`/`RememberFunctionNames`,
  `ResolveFunctionName` `:555`, `MapTurnContent` `FunctionCallContent` case `:509`,
  `GetResponseAsync` `:72`, `GetStreamingResponseAsync` `:127`)
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/GeminiInteractionsChatClientTests.cs`
  (`RecordingHandler`, `Throws_WhenFunctionResultNameCannotBeResolved`,
  `DoesNotPersist_FunctionNameAcrossClientInstances`)
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/GeminiInteractionsChatClientPhase2To4Tests.cs`
  (`CreateSseResponse`, `BuildEvent`, `CollectUpdatesAsync` — streaming test style)
- `tests/CatHerder.Agents.AI.Gemini.IntegrationTests/GeminiInteractionsLiveTests.cs`
  (`LiveGeminiFact` gating, raw-request style)
- Build/test commands from `project.instructions.md` (repo root): `dotnet build`,
  `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests`

## 1. Tasks

Allowed task statuses: not-started, in-progress, blocked, implemented, reviewed, completed.

| Status | Task |
|---|---|
| `implemented` | [Task P065-T01: record only response/stream calls; document the map](tasks/task065-01-recording-semantics.md) |
| `implemented` | [Task P065-T02: prune resolved entries after the carrying request completed](tasks/task065-02-prune-after-success.md) |
| `implemented` | [Task P065-T03: unit tests for recording + pruning rules](tasks/task065-03-unit-tests.md) |
| `implemented` | [Task P065-T04: gated live test (name is required) + full validation](tasks/task065-04-live-test-and-validation.md) |

## 2. Task Parallelism

All tasks touch `GeminiInteractionsChatClient.cs` and build on each other: run them
sequentially T01 → T02 → T03 → T04. T03 needs the recording (T01) and pruning (T02)
behavior; T04 needs everything in place.

## 3. Acceptance Criteria

- [x] Calls that arrive as request content are not recorded in the map (state bag key
  absent/empty for such requests), with a code comment stating the transcript-lookup
  invariant.
- [x] Map declaration and `ResolveFunctionName` carry the documented rationale
  (`function_result.name` required in practice, observed 400 text, prune-only-after-success).
- [x] After a non-streaming `GetResponseAsync` succeeds (response mapped), the entries for
  the results carried by that request are removed from the map.
- [x] After a streaming call delivers all updates, the entries for the carried results are
  removed; a failed or partially consumed stream keeps them.
- [x] A failed non-streaming request keeps its entries; a subsequent retry succeeds.
- [x] Unit tests added for: request-carried calls not recorded; non-stream success prunes;
  non-stream failure keeps + retry succeeds; stream completion prunes; stream failure keeps.
- [x] Existing unit tests pass unchanged in their assertions.
- [x] Gated live test added proving the API rejects `function_result` without `name`
  (skipped without `GOOGLE_API_KEY`/`GEMINI_INTERACTIONS_MODEL`).
- [x] `dotnet build` clean; `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` green;
  `bash .agents/skills/catherder-skills/plan-task-standards/scripts/validate.sh
  .catherder/plans/plan065-function-name-map-pruning` clean.
