---
type: task
description: "Task 065-04 — gated live test proving function_result.name is required; full validation pass"
status: implemented
created: 2026-10-03T18:50:52+02:00
updated: 2026-10-03T19:18:45+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards`

Resources:
- `plan065-spec.md` (outcome 5)
- `data/function-name-map-investigation.md` (section 3 — the original experiment)
- `tests/CatHerder.Agents.AI.Gemini.IntegrationTests/GeminiInteractionsLiveTests.cs`
  (`LiveGeminiFact`, `LiveGeminiConfiguration`, raw request style)

## Objective

Prove the external assumption the map exists for, and run the full validation pass.

## Scope

Included:
- One `[LiveGeminiFact]` in the integration project: trigger a tool call from the real
  API (declare a trivial function tool), then send a follow-up whose `function_result`
  parts omit `name`; assert the API rejects it with HTTP 400 and
  `function_response.name ... cannot be empty` in the body. Skipped without
  `GOOGLE_API_KEY`/`GEMINI_INTERACTIONS_MODEL` like the other live tests.
- Full validation: `dotnet build`, unit tests, plan validator.
Excluded:
- Any production code change; running the live test against the real API is NOT
  required (credentials absent by default — the test is added, not executed).

## Steps

1. Add the live test mirroring the raw-request style of
   `RawInteractionsRequest_WithMay2026ApiRevision_ReturnsStepsSchema` (declare one
   function tool, first request gets the call, second request sends the result without
   `name`, expect 400 with the name error).
2. `dotnet build` at repo root.
3. `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests`.
4. `bash .agents/skills/catherder-skills/plan-task-standards/scripts/validate.sh
   .catherder/plans/plan065-function-name-map-pruning` — clear errors.

## Verification

- `dotnet build` exits 0 with no new warnings.
- Unit test project green.
- Validator exits 0 for the plan folder.
- Manual check: integration test compiles and would be skipped without credentials
  (visible in `dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests` output as
  skipped).

---

Everything above this line is the task specification. Everything below is the
execution record.

# Execution

## Executor Notes
By: pi coding agent (GLM) @ 2026-10-03T19:18:45+02:00

Added `RawFollowUp_FunctionResultWithoutName_IsRejectedWith400` to the integration
project. It speaks raw Interactions JSON (the adapter refuses to build name-less results
by design): request 1 declares a `get_weather` function tool and captures the returned
interaction id + call id; request 2 sends the `function_result` without `name` and with
`previous_interaction_id`, and asserts HTTP 400 with `function_response.name` in the
body. Mirrors the investigation experiment (data section 3).

Beyond the task's skip-only requirement: this environment had `GOOGLE_API_KEY` and
`GEMINI_INTERACTIONS_MODEL` configured, so the live test actually ran against the real
API — and passed. The 400 behavior the whole plan rests on is re-verified as of today.
Only `BuiltInGoogleSearch_ReturnsLiveResponse` was skipped (its own built-in-tool
gating, pre-existing).

## Executor Verification
By: pi coding agent (GLM) @ 2026-10-03T19:18:45+02:00

- `dotnet build` (repo root) → 0 errors, 0 warnings.
- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` → 92/92 passed.
- `dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests` → 8 passed
  (including `RawFollowUp_FunctionResultWithoutName_IsRejectedWith400` against the live
  API), 1 skipped (`BuiltInGoogleSearch_ReturnsLiveResponse`, pre-existing gating).
- Validator: `bash .agents/skills/catherder-skills/plan-task-standards/scripts/validate.sh
  .catherder/plans/plan065-function-name-map-pruning` → 0 errors, 0 warnings (7 files).

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
