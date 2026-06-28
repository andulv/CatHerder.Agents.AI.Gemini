---
type: task
description: "Task 061-03 - Make SSE parsing and reducer state fail fast"
status: implemented
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:19:43+02:00
---
## Required Context

Load and follow these skills:
- `plan-task-standards`

Read:
- `../plan061-spec.md`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiSseEventReducer.cs`
- Existing streaming unit tests in `tests/CatHerder.Agents.AI.Gemini.UnitTests/`

## Objective

Make streaming SSE parsing fail visibly for malformed known current-schema data
while preserving ordered MEAI `ChatResponseUpdate` reconstruction for valid
streams.

## Scope

Included:
- Throw `GeminiProtocolException` for malformed SSE JSON frames.
- Throw for known current-schema events missing required payload, index, delta,
  delta type, or required content state.
- Keep unknown noncritical additive event types tolerated only with debug
  logging and no state mutation.
- Make final streamed function-call state fail if required final arguments
  remain invalid or incomplete.
- Add tests for fail-fast streaming cases and valid reconstruction.

Excluded:
- Non-streaming response parsing.
- Provider error `status:error` mapping, except preserving handoff for T06.
- Streaming fallback option behavior, handled by T05.

## Steps

1. Review `ProcessSseFrame` and `GeminiSseEventReducer`.
2. Classify each ignored SSE branch against the spec categories.
3. Replace malformed known current-schema branches with
   `GeminiProtocolException`.
4. Keep unknown additive event types debug-logged and ignored only when they do
   not mutate reducer state.
5. Ensure reducer finalization throws for incomplete required function-call or
   built-in-tool state.
6. Add tests for malformed JSON, non-object payload, missing event structure,
   invalid index, missing delta type, unsupported known delta shape, invalid
   final function arguments, and valid streaming reconstruction.

## Verification

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiInteractionsChatClientPhase2To4Tests` passes.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiSse` passes, if a focused filter exists after the task.
- Manual check: unknown additive events are the only ignored SSE events and are debug-logged.

---

Everything above this line is the task specification. Everything below is the execution record.

# Execution

## Executor Notes
By: Codex GPT-5 @ 2026-06-13T20:19:43+02:00

- Made malformed SSE frame JSON, non-object payloads, and data frames without event type throw `GeminiProtocolException`.
- Made known SSE events require payload, index, delta, delta type, and required delta fields.
- Changed unknown `step.start` and `step.delta` types to debug-log and ignore without creating reducer state.
- Made streamed function-call finalization throw when id, name, or valid JSON-object arguments remain incomplete.
- Preserved valid streaming reconstruction and usage updates.

## Executor Verification
By: Codex GPT-5 @ 2026-06-13T20:19:43+02:00

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiInteractionsChatClientPhase2To4Tests` passed. Unit tests: 31 passed. Integration test assembly had no matching tests for the filter.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiSse` passed. Unit tests: 8 passed. Integration test assembly had no matching tests for the filter.
- Manual check: unknown additive SSE event/delta types are ignored with debug logging and no state mutation; malformed known events throw.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
