---
type: task
description: "Task 064-02 — configurable pre-flight inline request size check (default 20 MB)"
status: implemented
created: 2026-10-02T20:20:00+02:00
updated: 2026-10-02T20:46:53+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards` (executor role)

Read: `plan064-spec.md` §3, §7 Q2. Code: `GeminiInteractionsChatClientOptions.cs`,
`GeminiInteractionsChatClient.CreateInteractionRequestMessage` / `SendInteractionRequestAsync`,
`GeminiApiException.cs`.

## Objective

A request whose serialized body exceeds the documented inline limit fails before sending,
with an error that names the actual size and the limit.

## Scope

Included:
- New option (e.g. `MaxInlineRequestBytes`, `long?`, default `20_000_000`, `null` = no check) with XML docs pointing to the Gemini docs.
- The check works on both the streaming and non-streaming paths, on the serialized body.
- Exception type: reuse `GeminiApiException` if it fits (no HTTP status) or add a small, documented exception type.
  The parent (plan220) will surface its message to the user, so the message must be readable.

Excluded: automatic fallback to the Files API.

## Steps

1. Add the option and the check where the request content is created.
2. One unit test: oversize request → exception, and the fake HTTP handler is never called.
   One test: `null` disables the check. (Product behaviour: protects against opaque provider failures.)

## Verification

- `dotnet build` and `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` exit 0.
- Manual: the exception message contains both sizes and mentions the 20 MB inline limit.

---

Everything above this line is the task specification. Everything below is the
execution record. These sections repeat per review round (e.g. `Executor Notes
(post-review)`, `Reviewer Verification (second pass)`).

# Execution

## Executor Notes
By: pi (gpt-5.x) @ 2026-10-02T20:46:53+02:00

Work done:

- `GeminiInteractionsChatClientOptions.MaxInlineRequestBytes` (`long?`, default
  `20_000_000`, `null` disables) with XML docs pointing at the Gemini audio docs and the
  20 MB inline limit.
- Pre-flight check in `CreateInteractionRequestMessage` — the single convergence point of
  the streaming and non-streaming paths — measuring the serialized UTF-8 body byte count
  before `StringContent` is created.
- New public `GeminiRequestSizeExceededException` (`ArgumentException` subclass; the failure
  is invalid input to the client, and there is no HTTP status to carry on
  `GeminiApiException`). Exposes `RequestedBytes` and `MaxInlineRequestBytes`. The message
  names both sizes and the documented 20 MB inline limit, so plan220 can surface it as-is.
- Tests (in `GeminiInteractionsChatClientTests.cs`): non-streaming oversize request throws
  with a fake handler that is never called; streaming path throws at first enumeration
  (same pre-send point, handler never called); `MaxInlineRequestBytes = null` sends a
  ~20 MB base64 payload (handler called once).

## Executor Verification
By: pi (gpt-5.x) @ 2026-10-02T20:46:53+02:00

- `dotnet build` → exit 0, "0 Warning(s) 0 Error(s)".
- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` → exit 0, "Passed! - Failed: 0,
  Passed: 87, Skipped: 0, Total: 87" (3 new tests).
- Manual (scratch console app, not committed): with `MaxInlineRequestBytes = 50`, the call
  throws `GeminiRequestSizeExceededException` with message
  "The serialized Gemini Interactions request is 100 bytes, which exceeds the inline request
  limit of 50 bytes. The Gemini Interactions API accepts at most 20 MB of inline content per
  request (https://ai.google.dev/gemini-api/docs/audio). ..." and a throwing fake handler
  recorded 0 calls.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
