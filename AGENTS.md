# Agent Entry Point

## This repo is CatHerder-enabled

This project follows the CatHerder method for plans and tasks, defined by the
`plan-task-standards` skill (loaded on demand). It is self-contained.

Read [.agents/project.instructions.md](.agents/project.instructions.md) first.
For CatHerder plan/task work, also read
[.agents/catherder.project.instructions.md](.agents/catherder.project.instructions.md).
These may add constraints but must not override the skill's method.

Plans live in [.catherder/plans/](.catherder/plans/). Current phase and roadmap
are in [project-status-roadmap.md](project-status-roadmap.md).

## Always-on guardrails

- Scope is this project only.
- Treat files outside the project root as read-only by default.
- Do not edit outside the project unless the user explicitly requests it.
- If multiple projects are open, confirm the active project root first.
