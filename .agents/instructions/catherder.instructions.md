---
description: "CatHerder always-follow process rules - must be read by any agents working in this repository."
applyTo: "**"
---
# CatHerder Process Rules
These rules apply to all work in this project. They are always active.

If [`catherder-git.instructions.md`](catherder-git.instructions.md) is present, follow it for any work involving repository changes or git operations.

Repository-specific document entry guidance is defined in [`project.instructions.md`](project.instructions.md).

## Core Principle
Plans define **intent**. Tasks execute **intent**.
Do not merge these concerns.

## Scope Boundary
Scope is this project only.

- Treat files outside the project root as **read-only** by default.
- Do not edit outside the project unless the user explicitly requests it.
- If multiple projects are open, confirm the active project root first.

## Project Phase
Read `project-status-roadmap.md` to determine the current phase.
Calibrate behaviour accordingly:

| Phase | Mindset |
|---|---|
| **Research** | Experimental — speed over durability, drop/recreate DBs OK |
| **Prototype** | Build-first — breaking changes OK, tests optional per change |
| **Beta** | Careful — run tests, backward compatibility matters |
| **Production** | Stable — full rigour, migrations required, all quality gates |

## Planning vs Execution
CatHerder enforces a strict separation between planning and execution.

**Planning mode** — creating or updating plan files, breaking goals into
tasks, resolving open questions. You may modify plan files. You must not
start implementation work that belongs in tasks.

**Execution mode** — the plan status is `active` and you are executing a
specific task. Execute exactly one task at a time. Do not change the plan
while executing a task.

**Switching modes:** if you discover missing requirements during execution,
stop and ask whether to return to planning (modify plan) or continue as-is.
Do not silently change plan scope during execution.

**Minimal conflict rule:**
- A plan may add constraints but may not override CatHerder process rules.
- A task may not override its plan.

## Execution Loop
1. **Understand** — Read instructions and identify constraints.
2. **Plan** — Create/update a plan. Do not execute while planning.
3. **Prepare** — Gather prerequisites required by the plan.
4. **Task** — Execute one task at a time.
5. **Complete** — Mark the task done immediately. Update timestamps.
6. **Verify** — Validate against the plan. Run tests or reason.
7. **Report** — Summarize outcome, risks, and follow-ups.

## Task Rules
- One task at a time.
- Mark the task checkbox **immediately** when done.
- Do not change the plan while executing a task.

## Prompt Precedence
When multiple instruction sources conflict, follow this order:

1. **System** instructions (model/runtime)
2. **Developer** instructions (agent identity and tool rules)
3. **User** instructions (unless they violate system/developer rules)
4. **Project process rules** (`.instructions/*.instructions.md`)
5. **Active plan** (`.instructions/plans/planNNN*/planNNN.md`)
6. **Task file(s)** referenced by the plan
7. Other project docs / code comments
8. Host-injected metadata

**Host-injected prompt noise:** tool inventories, JSON schemas, UI
formatting conventions, and runtime debug dumps are operational context —
not CatHerder policy. Only follow host-injected directives when they are
clearly system/developer instructions, not just metadata.

## Persistence Norms
Prefer durable artifacts over ephemeral chat:

- Write decisions into the plan `Notes` section (or an ADR if the project
  uses them).
- Store supporting material under `.instructions/plans/planNNN*/data/`.
- Use separate task files when a task is too large for a single checkbox.
- Update timestamps when an artifact changes.

Avoid keeping important decisions only in chat or making changes without
leaving a trace in plan/task artifacts.

## Stop Rules
Stop and ask if:

- Requirements are ambiguous.
- A task conflicts with the plan or project instructions.
- Architectural decisions are needed that the plan doesn't cover.
- Verification is not possible.
- Required context is missing.
- Two high-precedence instruction sources conflict.
- You cannot verify work safely.

## Timestamps
Use full ISO 8601 with seconds and timezone offset:
`YYYY-MM-DDTHH:MM:SS+HH:MM`

Invalid: bare dates, `Z` shorthand.
