---
type: plan-implementation
description: "Plan 063 - Gemini adapter follows MEAI reasoning-options conventions and supports turning thinking off"
status: active
created: 2026-10-01T14:20:00+02:00
updated: 2026-10-01T14:20:00+02:00
---
# Plan 063 Implementation — Reasoning Options Conventions

## 0. Required Context

- Spec: [plan063-spec.md](plan063-spec.md)

## 1. Tasks

Allowed task statuses: not-started, in-progress, blocked, implemented, reviewed, completed.

| Status | Task |
|---|---|
| `implemented` | [Task P063-T01: MapThinkingLevel conventions and off switch](tasks/task063-01-map-thinking-level.md) |

## 2. Task Parallelism

Single task.

## 3. Acceptance Criteria

- [ ] Typed `Reasoning.Effort` is authoritative (incl. None → minimal); the raw
      AdditionalProperties value is only an extension and never overrides a typed value.
- [ ] `reasoning.effort = "none"` and `reasoning.enabled = false` map to minimal.
- [ ] Unit tests cover each level, the extension fallback, the override guard and the off switch.
