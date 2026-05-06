---
type: reference
description: "Template for task files inside plan-local tasks/ folders"
snapshot_date: 2026-03-05
sources:
  - docs/catherder/plans-and-tasks.md
  - docs/catherder/file-formats.md
---
# Task Template (Plan-Local)

Use this for files like `tasks/task002-01-short-name.md`.

```markdown
---
type: task
description: "Task NNN-NN — one-line objective"
---
# Task NNN-NN: <Title>

**Status:** `not-started`

**Created:** YYYY-MM-DDTHH:MM:SS+HH:MM

**Updated:** YYYY-MM-DDTHH:MM:SS+HH:MM

## Task ID

NNN-NN

## Objective

What this task does.

## Scope

Included and excluded work.

## Steps

1. Step one.
2. Step two.

## Verification

How this task is verified in isolation.

## Notes

No plan changes allowed here.
```

## Naming

- Use `NNN` from parent plan.
- Use zero-padded two-digit task number `NN`.
- Use kebab-case short name after the number.
