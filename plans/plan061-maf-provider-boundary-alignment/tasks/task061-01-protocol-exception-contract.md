---
type: task
description: "Task 061-01 - Add Gemini protocol exception contract"
status: implemented
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:09:28+02:00
---
## Required Context

Load and follow these skills:
- `plan-task-standards`

Read:
- `../plan061-spec.md`
- `src/CatHerder.Agents.AI.Gemini/GeminiApiException.cs`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiSseEventReducer.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiBuiltInToolBridge.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiUsageMapper.cs`

## Objective

Add the public exception contract for Gemini provider protocol violations.

## Scope

Included:
- Add public `GeminiProtocolException : InvalidOperationException`.
- Include safe metadata properties for operation, SSE event type, JSON field or
  path, response id, and model id where practical.
- Preserve inner exceptions for lower-level parse failures.
- Add focused tests for construction and metadata behavior.

Excluded:
- Replacing every parser failure. Later tasks apply the exception contract to
  specific parsing paths.
- Adding raw payload storage to the exception.
- Changing `GeminiApiException`.

## Steps

1. Add `GeminiProtocolException` in the public package namespace.
2. Keep constructors minimal and useful for package consumers.
3. Ensure XML documentation distinguishes it from `GeminiApiException`.
4. Add unit tests for basic construction, metadata, and inner exception.
5. Confirm no raw provider payload property is exposed.

## Verification

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiProtocolException` passes, or the equivalent focused test filter for the added tests.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter CatHerder.Agents.AI.Gemini.UnitTests` passes.
- Manual check: `GeminiProtocolException` is public and `GeminiApiException` still handles HTTP/API failures.

---

Everything above this line is the task specification. Everything below is the execution record.

# Execution

## Executor Notes
By: Codex GPT-5 @ 2026-06-13T20:09:28+02:00

- Added public `GeminiProtocolException : InvalidOperationException`.
- Added safe metadata properties for operation name, SSE event type, JSON path, response id, and model id.
- Preserved lower-level exceptions through `InnerException`.
- Did not add any raw provider payload/body property.
- Left `GeminiApiException` unchanged for HTTP/API failures.

## Executor Verification
By: Codex GPT-5 @ 2026-06-13T20:09:28+02:00

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiProtocolException` passed. Unit tests: 4 passed. Integration test assembly had no matching tests for the filter.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter CatHerder.Agents.AI.Gemini.UnitTests` passed. Unit tests: 37 passed. Integration test assembly had no matching tests for the filter.
- Manual check: `GeminiProtocolException` is public and `GeminiApiException` remains the HTTP/API failure exception.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
