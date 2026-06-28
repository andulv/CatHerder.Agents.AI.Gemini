---
type: task
description: "Task 061-07 - Verify package boundary and cleanup stale tests"
status: implemented
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:28:23+02:00
---
## Required Context

Load and follow these skills:
- `plan-task-standards`

Read:
- `../plan061-spec.md`
- `../plan061-implementation.md`
- All task execution notes for P061-T01 through P061-T06
- `src/CatHerder.Agents.AI.Gemini/`
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/`

## Objective

Verify the final Gemini package boundary after all alignment tasks and remove
stale tests or documentation that contradict the new contract.

## Scope

Included:
- Run full package-local test suite.
- Search for stale legacy compatibility tests and runtime paths.
- Search for stale custom telemetry expectations.
- Confirm no CatHerder application dependency has been introduced.
- Confirm implementation matches acceptance criteria.

Excluded:
- CatHerder application changes.
- Live Gemini integration tests unless explicitly requested and credentials are
  configured.
- Package publishing.

## Steps

1. Review changes from T01 through T06.
2. Remove or update stale tests that assert legacy `outputs`, legacy usage
   aliases, silent parsing, silent fallback, or default Gemini-owned telemetry
   spans.
3. Verify public package surface for `GeminiApiException` and
   `GeminiProtocolException`.
4. Run package-local tests.
5. Run targeted searches for prohibited legacy paths and downstream app
   coupling.
6. Update the implementation acceptance criteria checkboxes only when all
   criteria are actually met.

## Verification

- `dotnet test CatHerder.Agents.AI.Gemini.slnx` passes, except live tests that are credential-gated and skipped by default.
- `rg -n "prompt_token_count|candidates_token_count|total_token_count|outputs" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` shows no runtime compatibility path or stale expected behavior.
- `rg -n "CatHerder\\.Agents\\.Core|CatHerder\\.Web|CatHerderTelemetry|CatHerder\\.Diagnostics" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` returns no package dependency.
- `rg -n "ActivitySource|GeminiBuiltInToolTelemetry|gen_ai.tool.call.arguments|gen_ai.tool.call.result" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` confirms no default ungated custom telemetry path remains.
- `bash /home/anders/source/agent/catherder-dev/.agents/skills/catherder-skills/plan-task-standards/scripts/validate.sh plans/plan061-maf-provider-boundary-alignment` exits 0 or only reports lifecycle warnings expected before review/completion.

---

Everything above this line is the task specification. Everything below is the execution record.

# Execution

## Executor Notes
By: Codex GPT-5 @ 2026-06-13T20:28:23+02:00

- Reviewed implemented task changes and removed stale custom telemetry expectations.
- Confirmed legacy usage aliases were not reintroduced.
- Confirmed legacy `outputs` support was not reintroduced; the remaining `outputs` occurrence is a negative test that verifies rejection.
- Confirmed no CatHerder application dependency was introduced.
- Checked all implementation acceptance criteria because final verification passed.

## Executor Verification
By: Codex GPT-5 @ 2026-06-13T20:28:23+02:00

- `dotnet test CatHerder.Agents.AI.Gemini.slnx` passed. Unit tests: 57 passed. Integration tests: 6 passed, 1 skipped.
- `rg -n "prompt_token_count|candidates_token_count|total_token_count|outputs" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` returned only `tests/CatHerder.Agents.AI.Gemini.UnitTests/GeminiInteractionsChatClientTests.cs:303`, the negative legacy `outputs` rejection test.
- `rg -n "CatHerder\\.Agents\\.Core|CatHerder\\.Web|CatHerderTelemetry|CatHerder\\.Diagnostics" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` returned no matches.
- `rg -n "ActivitySource|GeminiBuiltInToolTelemetry|gen_ai.tool.call.arguments|gen_ai.tool.call.result" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` returned no matches.
- `bash /home/anders/source/agent/catherder-dev/.agents/skills/catherder-skills/plan-task-standards/scripts/validate.sh plans/plan061-maf-provider-boundary-alignment` passed with 0 errors, 0 warnings, 11 files checked.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
