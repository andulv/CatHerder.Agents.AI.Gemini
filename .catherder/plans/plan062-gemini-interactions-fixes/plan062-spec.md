---
type: plan-spec
description: "Plan 062 - Gemini Interactions client fixes: thinking config, SSE errors, thought summaries, truncation warnings"
status: ready
created: 2026-07-01T00:20:20+02:00
updated: 2026-07-01T00:20:20+02:00
---
# Plan 062 Spec — Gemini Interactions Client Fixes

## 0. Required Context

- `plan-task-standards`
- `submodules/CatHerder.Agents.AI.Gemini/src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `submodules/CatHerder.Agents.AI.Gemini/src/CatHerder.Agents.AI.Gemini/Internal/GeminiSseEventReducer.cs`
- `submodules/CatHerder.Agents.AI.Gemini/src/CatHerder.Agents.AI.Gemini/Internal/GeminiInteractionsRequestModels.cs`
- `submodules/CatHerder.Agents.AI.Gemini/src/CatHerder.Agents.AI.Gemini/Internal/GeminiUsageMapper.cs`
- Gemini thinking docs: https://ai.google.dev/gemini-api/docs/thinking

## 1. Goal

Fix four gaps in the Gemini Interactions chat client that cause silent data
loss: reasoning config dropped before sending, standalone SSE errors swallowed,
readable thought summaries never requested, and truncated responses shown
without warning.

## 2. Context / Why

Discovered during CatHerder plan 109 (model parameter selection) and live
testing with Gemini 3.5 Flash. The UI lets users set reasoning effort and
max output tokens, but:
- Reasoning effort is never sent to the API (issue 1).
- API errors mid-stream produce empty assistant bubbles (issue 2).
- Thought summaries are never enabled, so the reasoning panel never fires
  despite the pipeline being ready (issue 5).
- Low max-output-tokens produces silently cut-short responses (issue 3).

Issue 4 (thought tokens excluded from pricing) is a CatHerder.Web issue and is
tracked out-of-scope here (see section 5).

## 3. What We Want To Achieve (Outcomes)

- When `ChatOptions.Reasoning.Effort` or
  `AdditionalProperties["reasoning.effort"]` is set, the request includes the
  corresponding thinking config.
- When a standalone `event: error` SSE frame arrives, the reducer emits an
  `ErrorContent` so CatHerder.Web's error pipeline surfaces it.
- Thought summaries are requested (`thinking_summaries: "auto"`) so the API
  sends readable `thought_summary` deltas that the existing `TextReasoningContent`
  pipeline can display.
- When the response is truncated (`status: "incomplete"`), the reducer emits a
  visible warning so the user knows the output was cut short.

## 4. Key Principles / Constraints

- Package code stays independent of CatHerder.Web — no cross-project dependencies.
- Offline unit tests remain the default feedback loop; live tests stay
  credential-gated and skipped by default.
- The Gemini Interactions API is preview/schema-evolving; prefer explicit
  compatibility choices.
- Backward compatible: `thinking_summaries: "auto"` is always sent (harmless
  for non-thinking models); `thinking_level` is only sent when reasoning effort
  is set.
- The usage mapper is correct per MEAI convention — thought token pricing is a
  caller concern, not a mapper concern.

## 5. Out of Scope

- CatHerder.Web pricing/display changes (issue 4: include reasoning tokens in
  output pricing and usage display). This is tracked separately — it applies to
  all providers that separate reasoning tokens, not just Gemini.
- CatHerder.Web truncation warning display (issue 3 web side). The submodule
  emits a warning content; the web side's existing error/warning pipeline will
  surface it. If additional web-side UX is needed, it is tracked separately.

## 6. Implementation Notes

- Thinking config and thinking summaries both modify
  `GeminiInteractionGenerationConfig` and `MapGenerationConfig` — combine into
  one task.
- The SSE error handler and truncation warning are reducer-only changes —
  independent tasks that can run in parallel with the request-config task.
- The existing `thought_summary` handler in the reducer
  (`GeminiSseEventReducer.cs:192-205`) already emits `TextReasoningContent`
  correctly; it just needs the API to actually send summaries (via
  `thinking_summaries: "auto"`).

## 7. Open Questions

1. Should `thinking_summaries` always be `"auto"` or only when reasoning is enabled? (resolved: always `"auto"` — harmless for non-thinking models and simplifies the code path.)
2. Should the truncation warning be an `ErrorContent` or a `TextContent`? (resolved: `TextContent` — it is not an error, just an incomplete response. The finish reason is already `ChatFinishReason.Length`.)
