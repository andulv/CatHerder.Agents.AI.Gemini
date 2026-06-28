---
type: plan
description: "Plan 060 draft - Make Gemini Interactions schema parsing fail fast"
status: draft
---

# Plan 060 Draft: Gemini Interactions Fail-Fast Schema Parsing

**Status:** draft
**Created:** 2026-06-13T01:37:15+02:00
**Updated:** 2026-06-13T01:37:15+02:00

## Goal

Make Gemini Interactions response and streaming parsing explicit and fail-fast
for current-schema violations, so provider/API drift is visible instead of
being silently converted into empty or partial chat output.

## Context / Why

The package now targets the current Gemini Interactions `steps` schema and
current Interactions usage fields. Legacy `outputs` support is intentionally
removed, and legacy usage-token aliases have been removed from runtime mapping.

The remaining quality issue is overly defensive parsing. Several current-schema
response and SSE paths currently ignore malformed data, unsupported content, or
missing required fields. That is bad for a provider adapter because it can hide
schema drift, corrupt streams, incomplete function calls, and unsupported caller
requests.

This package is still in prototype phase, so clean, debuggable behavior is more
important than broad compatibility.

## What We Want To Achieve (Outcomes)

- Current Interactions response mapping throws on malformed required structure.
- Current Interactions SSE parsing throws or surfaces explicit errors for
  malformed current-schema events.
- Function calls with missing ids, names, or final invalid arguments fail
  visibly instead of disappearing.
- Malformed SSE JSON frames fail the stream instead of being ignored.
- Unsupported caller options such as unknown `ChatResponseFormat` variants fail
  visibly instead of being silently ignored.
- Known intentionally optional provider fields remain optional.
- Legacy schema support is not reintroduced.

## Current Review Findings

No runtime source references to legacy usage-token fields remain under
`src/`.

Relevant current defensive paths:

- `GeminiInteractionsChatClient.MapInteractionToChatResponse()` skips
  non-object steps.
- `GeminiInteractionsChatClient.MapStep()` returns on missing step type and
  silently ignores unknown step types.
- `GeminiInteractionsChatClient.MapModelOutputStep()` returns on missing
  `model_output.content`, skips non-object content blocks, and silently ignores
  unknown content block types.
- `GeminiInteractionsChatClient.AddFunctionCallContent()` returns when a
  function call is missing id or name.
- `GeminiInteractionsChatClient.ProcessSseFrame()` ignores non-object or
  malformed JSON SSE payloads.
- `GeminiInteractionsChatClient.MapResponseFormat()` maps unsupported
  `ChatResponseFormat` values to `null`.
- `GeminiSseEventReducer` returns when required event structure is missing,
  such as missing `index`, missing `delta`, or missing delta `type`.
- `GeminiSseEventReducer` ignores unsupported delta types.
- `GeminiSseEventReducer.GetFunctionArguments()` treats invalid accumulated
  function-call arguments as incomplete and returns null, even during final
  flush paths.
- `GeminiSseEventReducer.TryGetIndex()` and `TryGetString()` swallow invalid
  values and return false/null.

## Key Principles / Constraints

- Support only the current Interactions `steps` schema.
- Do not add legacy field lists, legacy compatibility paths, or tests for legacy
  usage fields.
- Unknown noncritical SSE event types may remain tolerated if the Interactions
  API can emit additive lifecycle events that do not affect chat output.
- Malformed known event types should not be ignored.
- Missing required fields in known schema objects should throw with enough
  context to diagnose the provider payload.
- Mid-stream partial function-call arguments may be incomplete temporarily, but
  finalization must fail if arguments never become valid JSON.
- Keep offline unit tests as the default verification path.
- Live integration tests remain credential-gated and skipped by default.
- Avoid new proxy/dispatcher abstractions. Prefer direct parsing helpers where
  they clarify required-vs-optional fields.

## Out Of Scope

- Reintroducing support for legacy `outputs` schema.
- Reintroducing legacy GenerateContent usage-token aliases.
- Redesigning the request DTO model.
- Reworking built-in tool telemetry beyond making malformed provider data
  visible.
- Changing CatHerder main application code.
- Running live Gemini integration tests unless explicitly requested.

## Implementation Notes

Implementation direction only. Do not execute yet.

- Replace silent `return`, `continue`, or debug-only ignores in current-schema
  response parsing with explicit exceptions when the field is required for the
  current schema.
- Keep helper names semantic: `GetRequiredString`, `GetOptionalString`,
  `GetRequiredIndex`, `GetRequiredObject`, and similar. Avoid generic
  `TryGet...` helpers where invalid data should fail.
- Split stream behavior between:
  - tolerated unknown event type;
  - malformed known event type;
  - valid partial event that is not yet complete.
- Add finalization checks for in-flight function calls and built-in tool events.
- Update unit tests to prove malformed current-schema responses/frames fail and
  valid current-schema responses still pass.

## Open Questions

1. Should unsupported unknown SSE event types remain debug-only ignored, or
   should they be warnings?
2. Should stream failures throw exceptions directly, or should some known
   provider error events emit `ErrorContent` and then stop?
