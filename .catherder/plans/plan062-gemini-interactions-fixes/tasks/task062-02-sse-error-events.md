---
type: task
description: "Task 062-02 — Handle standalone SSE error events in reducer"
status: completed
created: 2026-07-01T00:20:20+02:00
updated: 2026-07-01T00:30:42+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards`

## Objective

Add a `case "error":` handler to `GeminiSseEventReducer.Reduce` so standalone
SSE error frames are surfaced as `ErrorContent` instead of being silently
swallowed by the `default` branch.

## Scope

Included:
- Add `case "error":` to the switch in `GeminiSseEventReducer.Reduce`
  (`GeminiSseEventReducer.cs:28-60`).
- Parse the payload's `error` object (message, code/status).
- Emit an `ErrorContent` update, matching the existing pattern used by
  `HandleStatusUpdate` for `status == "error"` (`GeminiSseEventReducer.cs:83-104`).
- Log at Warning level.

Excluded:
- Changes to the existing `HandleStatusUpdate` error path (it already works).
- CatHerder.Web error display changes (the web pipeline already handles
  `ErrorContent`).

## Steps

1. Add `case "error":` to the switch in `Reduce`, calling a new
   `HandleErrorEvent(payload, updates)` method.
2. In `HandleErrorEvent`, parse `payload["error"]` as a `JsonObject` (fallback
   to `payload` itself if no `error` key).
3. Extract `message` and `code`/`status` using the same `OptionalString`
   helpers as `HandleStatusUpdate`.
4. Create an `ErrorContent` with `ErrorCode` and `Details`, matching the
   `HandleStatusUpdate` pattern exactly.
5. Log at Warning: `"Gemini streaming error event received: {Message}"`.
6. Write a unit test that feeds a standalone `event: error` frame and asserts
   an `ErrorContent` is emitted.

## Verification

- `dotnet build` exits 0.
- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` exits 0.
- Unit test: when `Reduce("error", payload)` is called with
  `{"error":{"message":"high demand","code":"api_error"}}`, it returns an
  update containing `ErrorContent` with `Message="high demand"` and
  `ErrorCode="api_error"`.
- Unit test: the existing `HandleStatusUpdate` error path still works
  (regression check).

---

Everything above this line is the task specification. Everything below is the execution record. These sections repeat per review round.

# Execution

## Executor Notes
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

- Added `case "error":` to `GeminiSseEventReducer.Reduce` calling new `HandleErrorEvent`.
- `HandleErrorEvent` parses `payload["error"]` (or `payload` itself), extracts `message`/`code`/`status`, and emits `ErrorContent` matching the existing `HandleStatusUpdate` pattern.
- Logs at Warning level.
- Added unit test: standalone `event: error` frame emits `ErrorContent` with message and code.

## Executor Verification
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

`dotnet test` → 63/63 passed. Existing `ErrorContent_StreamingStatusError` regression check passes.

## Reviewer Verification
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

Self-verified. Accepted.

## Review Notes
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

Accepted. Issue 2 resolved.
