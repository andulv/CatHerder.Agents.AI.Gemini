---
type: task
description: "Task 064-02 — configurable pre-flight inline request size check (default 20 MB)"
status: not-started
created: 2026-10-02T20:20:00+02:00
updated: 2026-10-02T20:20:00+02:00
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
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Executor Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
