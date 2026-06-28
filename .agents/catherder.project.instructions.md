---
type: project
description: "CatHerder.Agents.AI.Gemini — project-specific CatHerder rules"
---
# CatHerder.Agents.AI.Gemini — CatHerder Project Rules

The CatHerder **method** (planning/execution, roles, validation, formats) is owned
by the `plan-task-standards` skill, loaded on demand from
`.agents/skills/catherder-skills/plan-task-standards/`. This file holds
**project-specific** rules only; it may add constraints but must not override the
skill's method.

General project guidance is in [`project.instructions.md`](project.instructions.md).

## Scope Boundary

Scope is this project only.

- Treat files outside the project root as read-only by default.
- Do not edit outside the project unless the user explicitly requests it.
- If multiple projects are open, confirm the active project root first.

## Project Phase

Read `project-status-roadmap.md` to determine the current phase (Research /
Prototype / Beta / Production) and calibrate behaviour accordingly.

## Plans

Plans live in `.catherder/plans/`.
