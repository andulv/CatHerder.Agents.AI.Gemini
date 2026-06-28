---
type: reference
description: "Example AGENTS.md snippet for projects that adopt the plan-task-standards skill"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Example Project AGENTS.md

Copy the block below into a project's own `AGENTS.md` to adopt this skill. It is
the always-on entry point: it points agents at the skill for plan/task work and
carries the few guardrails that must apply even when the skill is not loaded.

It intentionally contains **no** plan/task method details (those live in the
skill) and **no** git or build rules (those live in project CatHerder guidance).

---

```markdown
# Agent Entry Point

## This repo is CatHerder-enabled

This project follows the CatHerder method for plans and tasks, defined by the
`plan-task-standards` skill. For any planning, task execution, review, or
plan/task validation work, follow that skill. It is self-contained.

Read [`.agents/project.instructions.md`](.agents/project.instructions.md) first.
For CatHerder plan/task work, also read
[`.agents/catherder.project.instructions.md`](.agents/catherder.project.instructions.md).
These may add constraints but must not override the skill's method.

Plans live in [`.catherder/plans/`](.catherder/plans/). Each plan has its
own `planNNN-short-description/` directory with its spec, implementation, tasks,
reviews, and data.

## Always-on guardrails

These apply to all work, including ad-hoc edits outside any plan or task.

**Scope boundary**

- Scope is this project only.
- Treat files outside the project root as read-only by default.
- Do not edit outside the project unless the user explicitly requests it.
- If multiple projects are open, confirm the active project root first.

**Stop and ask** if:

- Requirements are ambiguous.
- A task conflicts with its plan or with project instructions.
- Architectural decisions are needed that the plan does not cover.
- Verification is not possible or required context is missing.
- Two high-precedence instruction sources conflict.
```

---

## Migrating an existing project

Older CatHerder projects keep their rules in copied process instruction files
(e.g. `catherder.instructions.md` loaded always via `applyTo: "**"`). To adopt
this skill cleanly:

1. Add the block above to the project's `AGENTS.md`.
2. Move any still-relevant project specifics (identity, phase, git, build/test)
   into `.agents/catherder.project.instructions.md`.
3. **Delete** any copied CatHerder process instruction files.

Deleting the legacy files is what prevents an agent using this skill from
accidentally following stale always-on rules in an unconverted project.
