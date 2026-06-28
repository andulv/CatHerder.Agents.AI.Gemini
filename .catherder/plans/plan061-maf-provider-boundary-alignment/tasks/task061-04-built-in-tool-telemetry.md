---
type: task
description: "Task 061-04 - Align built-in tool content and telemetry behavior"
status: implemented
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:21:40+02:00
---
## Required Context

Load and follow these skills:
- `plan-task-standards`

Read:
- `../plan061-spec.md`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiBuiltInToolBridge.cs`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- Built-in tool tests in `tests/CatHerder.Agents.AI.Gemini.UnitTests/`

## Objective

Align provider-side Gemini built-in tool observations with MEAI content and
canonical OpenTelemetry behavior.

## Scope

Included:
- Preserve built-in tool calls/results as standard MEAI
  `FunctionCallContent` / `FunctionResultContent` with `InformationalOnly`.
- Preserve Gemini marker metadata needed to avoid treating built-in tools as
  application-invoked functions.
- Remove default Gemini-owned `ActivitySource` and built-in-tool
  `execute_tool` spans from the standard provider path.
- Remove or revise tests that assert custom Gemini telemetry spans.
- Add tests proving built-in tool content remains observable as MEAI content.

Excluded:
- Adding a new opt-in diagnostic source.
- CatHerder diagnostics projection/storage.
- Changing normal application-invoked MEAI function call handling.

## Steps

1. Remove default use of `GeminiTelemetry.ActivitySource` and
   `GeminiBuiltInToolTelemetry` from request/stream processing.
2. Delete or reduce custom telemetry types that no longer serve package-local
   behavior.
3. Keep built-in tool content mapping and marker metadata.
4. Ensure raw built-in tool arguments/results are not emitted as package-owned
   OpenTelemetry tags by default.
5. Update tests to assert MEAI content semantics instead of custom activity
   spans.

## Verification

- `rg -n "ActivitySource|GeminiTelemetry|GeminiBuiltInToolTelemetry|gen_ai.tool.call.arguments|gen_ai.tool.call.result" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` shows no default custom Gemini telemetry path or only intentional comments/tests for absence.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter BuiltInTool` passes, if a focused filter exists.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter CatHerder.Agents.AI.Gemini.UnitTests` passes.
- Manual check: built-in tool observations remain informational MEAI content.

---

Everything above this line is the task specification. Everything below is the execution record.

# Execution

## Executor Notes
By: Codex GPT-5 @ 2026-06-13T20:21:40+02:00

- Removed default `GeminiTelemetry` / `GeminiBuiltInToolTelemetry` use from non-streaming and streaming paths.
- Deleted the package-owned built-in tool `ActivitySource` telemetry implementation.
- Preserved Gemini built-in tool calls/results as informational MEAI `FunctionCallContent` / `FunctionResultContent`.
- Updated tests to assert MEAI content semantics instead of custom activity spans.

## Executor Verification
By: Codex GPT-5 @ 2026-06-13T20:21:40+02:00

- `rg -n "ActivitySource|GeminiTelemetry|GeminiBuiltInToolTelemetry|gen_ai.tool.call.arguments|gen_ai.tool.call.result" src/CatHerder.Agents.AI.Gemini tests/CatHerder.Agents.AI.Gemini.UnitTests` returned no matches.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter BuiltInTool` passed. Unit tests: 5 passed. Integration test assembly had no matching tests for the filter.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter CatHerder.Agents.AI.Gemini.UnitTests` passed. Unit tests: 54 passed. Integration test assembly had no matching tests for the filter.
- Manual check: built-in tool observations remain informational MEAI content and no package-owned raw argument/result telemetry remains.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
