---
type: reference
description: "Template for the plan spec file (planNNN-spec.md) — defines intent"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Spec Template

Use for `planNNN-spec.md`. The spec defines **intent**: goal, outcomes,
constraints. Implementation details are out of scope here (capture only
direction under section 6). The spec replaces the older `planNNN-draft.md`.

```markdown
---
type: plan-spec
description: "Plan NNN - one-line summary of intent"
status: draft
created: YYYY-MM-DDTHH:MM:SS+HH:MM
updated: YYYY-MM-DDTHH:MM:SS+HH:MM
---
# Plan NNN Spec — <Title>

## 0. Required Context

- `<skill-name>`
- `<path or resource needed to understand this plan>`

## 1. Goal

What outcome this plan targets.

## 2. Context / Why

Why this is needed now.

## 3. What We Want To Achieve (Outcomes)

- Outcome 1
- Outcome 2

## 4. Key Principles / Constraints

- Constraint 1

## 5. Out of Scope

- Not doing X

## 6. Implementation Notes

Direction and hints only. Do not commit to tasks here.

## 7. Open Questions

1. <question> (open)
2. <question> (deferred)
3. <question> (resolved: <answer>)
```

Rules:

- `status` is `draft` while questions are open, `ready` once the
  open-questions gate passes (see below).
- Number every open question. Mark each `(resolved …)`, `(answered)`,
  `(deferred)`, `(skipped)`, or leave it open.
- **Open-questions gate:** do not create the implementation file while any
  open question is unresolved. `ready` requires zero unresolved questions.
- Extra sections are allowed; prefix them `A.`, `B.`, … after section 7.
