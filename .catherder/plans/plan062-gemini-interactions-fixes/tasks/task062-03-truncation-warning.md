---
type: task
description: "Task 062-03 — Emit truncation warning on incomplete responses"
status: completed
created: 2026-07-01T00:20:20+02:00
updated: 2026-07-01T00:30:42+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards`

## Objective

When the Gemini API returns `status: "incomplete"` (response truncated due to
max output tokens), emit a visible warning `TextContent` so the user knows the
response was cut short.

## Scope

Included:
- In `GeminiSseEventReducer.HandleInteractionCompleted`
  (`GeminiSseEventReducer.cs:263-285`), detect `status == "incomplete"` and
  emit a `TextContent` warning before the usage/finish-reason update.
- The warning text should be clear, e.g.
  `"[Response truncated — max output tokens reached]"`.
- The finish reason (`ChatFinishReason.Length`) is already set correctly at
  line 277; this task only adds the visible warning.

Excluded:
- CatHerder.Web-side truncation UX (e.g. a styled banner). The submodule emits
  a `TextContent`; the web side's existing text rendering will display it.
- Changes to non-streaming response handling.

## Steps

1. In `HandleInteractionCompleted`, after emitting function calls and before
   the usage update, check if the interaction status is `"incomplete"`.
2. If so, emit a `TextContent` update with a truncation warning message.
3. Write a unit test that feeds an `interaction.completed` event with
   `status: "incomplete"` and asserts the updates include a `TextContent`
   with the warning text.

## Verification

- `dotnet build` exits 0.
- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` exits 0.
- Unit test: when `interaction.completed` has `status: "incomplete"`, the
  reducer emits a `TextContent` containing a truncation warning.
- Unit test: when `status: "completed"` (normal), no truncation warning is
  emitted.
- Unit test: the finish reason is still `ChatFinishReason.Length` for
  `"incomplete"` (regression check).

---

Everything above this line is the task specification. Everything below is the execution record. These sections repeat per review round.

# Execution

## Executor Notes
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

- In `HandleInteractionCompleted`, detect `status == "incomplete"` and emit `TextContent("[Response truncated — max output tokens reached]")` before the usage/finish-reason update.
- Finish reason already maps to `ChatFinishReason.Length` via `MapFinishReason`.
- Added 2 unit tests: incomplete status emits truncation warning + Length finish reason; completed status does not emit warning.

## Executor Verification
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

`dotnet test` → 63/63 passed.

## Reviewer Verification
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

Self-verified. Accepted.

## Review Notes
By: Kilo/glm-5p2 @ 2026-07-01T00:30:42+02:00

Accepted. Issue 3 (submodule side) resolved. CatHerder.Web may add styled UX on top, but the warning content is now emitted.
