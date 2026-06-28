---
type: reference
description: "Template for the plan implementation file (planNNN-implementation.md) — the executable plan"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Implementation Template

Use for `planNNN-implementation.md`. This is the executable plan. It does **not**
copy the spec — it references it and adds only what execution needs. Replaces
the older `planNNN.md`.

```markdown
---
type: plan-implementation
description: "Plan NNN - one-line summary"
status: active
created: YYYY-MM-DDTHH:MM:SS+HH:MM
updated: YYYY-MM-DDTHH:MM:SS+HH:MM
---
# Plan NNN Implementation — <Title>

## 0. Required Context

- Spec: `planNNN-spec.md`
- `<skill-name>`
- `<other resource needed to execute>`

## 1. Tasks

Allowed task statuses: not-started, in-progress, blocked, implemented, reviewed, completed.

| Status | Task |
|---|---|
| `not-started` | [Task PNNN-T01: short objective](tasks/taskNNN-01-short-name.md) |
| `not-started` | [Task PNNN-T02: short objective](tasks/taskNNN-02-short-name.md) |

## 2. Task Parallelism

Which tasks may run in parallel and what depends on what.

## 3. Acceptance Criteria

- [ ] Criterion 1
- [ ] Criterion 2
```

Rules:

- `status`: `active` while executing; `completed` when all task rows are
  `completed` and every acceptance checkbox is checked; `abandoned` (with a
  reason) if dropped.
- Goal/context live in the spec — do not duplicate them here.
- `## 1. Tasks` is a status table (never checkboxes); column 1 is the status in
  backticks, column 2 links to the task file. Human task IDs use `PNNN-TNN`.
- `## 3. Acceptance Criteria` uses checkboxes.
