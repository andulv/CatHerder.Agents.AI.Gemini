---
type: task
description: "Task 061-05 - Align caller options and streaming fallback behavior"
status: implemented
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:24:45+02:00
---
## Required Context

Load and follow these skills:
- `plan-task-standards`

Read:
- `../plan061-spec.md`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClientOptions.cs`
- Existing option and streaming fallback tests

## Objective

Make unsupported caller options fail visibly and remove silent streaming
fallback by default.

## Scope

Included:
- Make unsupported `ChatResponseFormat` variants throw instead of silently
  degrading.
- Ensure other unsupported caller options fail visibly where the provider
  cannot honor them.
- Make SSE negotiation failure fail by default.
- Retain non-streaming retry only if an explicit opt-in provider option already
  exists or is intentionally added as disabled by default.
- Add tests for default failure and opt-in behavior if retained.

Excluded:
- Parser fail-fast changes handled by T02/T03.
- Provider `status:error` mapping handled by T06.
- Live integration testing unless explicitly requested.

## Steps

1. Review `MapResponseFormat` and all option mapping paths.
2. Identify unsupported options that currently silently degrade.
3. Throw clear exceptions for unsupported caller requests.
4. Review `GeminiInteractionsChatClientOptions` and streaming fallback code.
5. Remove silent fallback or require an explicit disabled-by-default option.
6. Add tests for unsupported response format and SSE negotiation behavior.

## Verification

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter ResponseFormat` passes, if a focused filter exists.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter StreamingFallback` passes, if a focused filter exists.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter CatHerder.Agents.AI.Gemini.UnitTests` passes.
- Manual check: default streaming path does not retry non-streaming after SSE negotiation failure.

---

Everything above this line is the task specification. Everything below is the execution record.

# Execution

## Executor Notes
By: Codex GPT-5 @ 2026-06-13T20:24:45+02:00

- Removed default non-streaming retry after SSE negotiation failure.
- Added public `GeminiSseNegotiationException : HttpRequestException` for failures before an SSE stream is established.
- Made unsupported `ChatResponseFormat` variants throw `NotSupportedException`.
- Made unsupported non-function `ChatOptions.Tools` entries throw instead of being silently ignored.

## Executor Verification
By: Codex GPT-5 @ 2026-06-13T20:24:45+02:00

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter ResponseFormat` passed. Unit tests: 2 passed. Integration test assembly had no matching tests for the filter.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter StreamingFallback` passed. Unit tests: 1 passed. Integration test assembly had no matching tests for the filter.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter CatHerder.Agents.AI.Gemini.UnitTests` passed. Unit tests: 55 passed. Integration test assembly had no matching tests for the filter.
- Manual check: default streaming path now throws on SSE negotiation failure and does not retry non-streaming.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
