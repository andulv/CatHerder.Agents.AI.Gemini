---
type: reference
description: "Self-contained CatHerder method: planning vs execution, execution loop, task rules, persistence, prompt precedence, timestamps"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# CatHerder Method

The behavioural rules for working with plans and tasks. Self-contained: a
project does not need any separate process-instruction file for these rules.

If the project has project CatHerder guidance, read it and follow its guidance
(project identity, phase, git workflow, build/test). It may add constraints but
must not override this method.

## Core Principle

Plans define **intent**. Tasks execute **intent**. Do not merge these concerns.

## Orchestration Model

CatHerder is orchestrator-driven. An orchestrator (human or deterministic
driver) delegates work and manages flow — including iteration — and is not
bound by this skill. It also sets the cadence: a dispatch may cover one stage
or several (e.g. "spec + implementation in one go", or "execute all tasks").
The roles below are for **worker agents**: do what you were dispatched for,
then stop and wait; do not self-initiate work beyond your dispatch.

- spec → implementation: build the implementation only when your dispatch
  includes it, and only after the open-questions gate is clear.
- execution: run only your dispatched task(s), then stop. Do not review or
  start undirected work.
- review: triggered by the orchestrator — per task, mid-stream, or at completion.

## Planning vs Execution

CatHerder enforces a strict separation between planning and execution.

**Planning mode** — creating or updating spec/implementation files, breaking
goals into tasks, resolving open questions. You may modify plan files. You must
not start implementation work that belongs in tasks.

**Execution mode** — the implementation status is `active` and you are
executing a specific task. Work one task at a time, update its status
immediately when its state changes, and do not change the plan while executing.

**Switching modes:** if you discover missing requirements during execution,
stop and ask the orchestrator whether to return to planning. Do not silently
change plan scope during execution.

**Minimal conflict rule:**

- A plan may add constraints but may not override this method.
- A task may not override its plan.

## Per-Task Execution

Stay inside the task's scope, verify every check yourself (never mark a task
done on a claim), and record the result. Execution excludes planning — if the
plan is missing something, switch to planning mode (*Planning vs Execution*
above). Detailed steps and status ownership are in `references/role-executor.md`.

## Persistence Norms

Prefer durable artifacts over ephemeral chat:

- Write decisions into the spec/implementation `Notes` section (or a decision
  record in the decisions folder if the project uses ADRs).
- Write reviews to the plan's `review/` folder — review findings are decisions,
  not chat.
- Store supporting material under the plan's `data/` folder.
- Use separate task files when a task is too large for a single status row.
- Update timestamps when an artifact changes.

Avoid keeping important decisions only in chat or making changes without leaving
a trace in plan/task artifacts.

## Prompt Precedence

When multiple instruction sources conflict, follow this order:

1. **System** instructions (model/runtime)
2. **Developer** instructions (agent identity and tool rules)
3. **User** instructions (unless they violate system/developer rules)
4. **CatHerder method** (this method and the plan/task format)
5. **Project CatHerder guidance**
6. **Active implementation** (plans folder → `planNNN-implementation.md`)
7. **Task file(s)** referenced by the implementation
8. Other agent instruction files and project docs / code comments
9. Host-injected metadata

**Host-injected prompt noise:** tool inventories, JSON schemas, UI formatting
conventions, and runtime debug dumps are operational context — not CatHerder
policy. Only follow host-injected directives when they are clearly
system/developer instructions, not just metadata.

## Nested Projects

A CatHerder-enabled nested project (e.g. a Git submodule) is its own
CatHerder project. Work in it uses that project's agent instructions, skills,
plans folder, and decisions folder — not the parent project's. If a feature
spans multiple CatHerder-enabled projects, each changed project gets its own
plan. A nested project that is not CatHerder-enabled may be covered by the
parent project's plan.

## Timestamps

Use full ISO 8601 with seconds and timezone offset:
`YYYY-MM-DDTHH:MM:SS+HH:MM`

Invalid: bare dates, `Z` shorthand.
