---
type: plan-spec
description: "Plan 063 - Gemini adapter follows MEAI reasoning-options conventions and supports turning thinking off"
status: ready
created: 2026-10-01T14:05:00+02:00
updated: 2026-10-01T14:05:00+02:00
---
# Plan 063 Spec — Reasoning Options Conventions

## 0. Required Context

- `plan-task-standards`; this submodule's `.agents/` instructions.
- Parent effort: catherder-dev plan216 (model parameter wire fidelity), task T05 and its
  data files. The parent repo records the cross-project decision; this plan covers the
  submodule change.
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs` (`MapThinkingLevel`,
  `MapChatOptionsToGenerationConfig`).
- Parent-repo investigation: `GeminiInteractionsChatClient.MapThinkingLevel` reads only
  `AdditionalProperties["reasoning.effort"]` plus the typed enum subset; no off switch.

## 1. Goal

The adapter maps reasoning options the way MEAI adapters do: the typed
`ChatOptions.Reasoning` is authoritative; `AdditionalProperties` is an extension channel for
values the typed model cannot express; and an explicit off request disables thinking.

## 2. Context / Why

CatHerder (the main consumer) offers reasoning parameters per model and now guarantees that
every offered parameter reaches the wire (parent plan216). The Gemini adapter is the only
consumer-facing gap: it ignores the off switch, and its effort mapping prefers a
CatHerder-private AdditionalProperties key over the typed `Reasoning`. As maintainers we fix
convention deviations in the package itself.

## 3. What We Want To Achieve (Outcomes)

- `MapThinkingLevel` maps the typed `Reasoning.Effort` (none/low/medium/high/xhigh) first,
  and falls back to `AdditionalProperties["reasoning.effort"]` only for levels outside the
  typed enum (e.g. "minimal"), never overriding an explicit typed value with a missing raw
  value.
- `Reasoning.Effort = None` (and `reasoning.enabled=false` in AdditionalProperties) maps to
  the Interactions generation config's thinking-off level (verify the field/value against the
  current Interactions API; e.g. `thinking_level: "off"` or its documented equivalent) so a
  per-request off switch exists where the model supports it.
- Temperature, max output tokens and thinking summaries keep their current mapping.
- Unit tests pin: typed effort mapping per level, the raw fallback for a non-typed level, and
  the off switch.

## 4. Key Principles / Constraints

- MEAI conventions first: typed options authoritative; AdditionalProperties only as
  documented extension channel.
- No breaking change for existing consumers that set only the raw key (it still works for
  non-typed values).
- Wire field names verified against current Gemini Interactions API docs before use.

## 5. Out of Scope

- Thinking budgets (no Interactions equivalent identified).
- Multi-level effort beyond what the API documents.

## 6. Implementation Notes

- One method (`MapThinkingLevel`) plus its call site; tests in
  `tests/CatHerder.Agents.AI.Gemini.UnitTests`.

## 7. Open Questions

1. Exact Interactions field/value for thinking off (resolved during implementation: use the
   documented generation-config value; if none exists, map to the lowest documented level and
   record it here).
