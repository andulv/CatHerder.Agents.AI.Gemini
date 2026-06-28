---
type: reference
description: "Template for task files inside plan-local tasks/ folders"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Task Template (Plan-Local)

Use this for files like `tasks/task002-01-short-name.md`.

```markdown
---
type: task
description: "Task NNN-NN — one-line objective"
status: not-started
created: YYYY-MM-DDTHH:MM:SS+HH:MM
updated: YYYY-MM-DDTHH:MM:SS+HH:MM
---
## Required Context
Load and follow these skills:
- `<skill-name>`

## Objective

What this task does.

## Scope

Included and excluded work.

## Steps

1. Step one.
2. Step two.

## Verification

- `<command or search check>` exits 0 / expected result.
- Manual check: <specific yes/no condition>.

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

```

## Naming

- Use `NNN` from parent plan.
- Use zero-padded two-digit task number `NN`.
- Use kebab-case short name after the number.
