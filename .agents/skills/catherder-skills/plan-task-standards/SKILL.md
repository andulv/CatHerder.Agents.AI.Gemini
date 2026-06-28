---
name: plan-task-standards
description: "Self-contained CatHerder method and standards for plans and tasks: planning vs execution, execution loop, prompt precedence, persistence; plus prompt/spec/implementation/review/follow-up files, frontmatter, statuses, timestamps, the open-questions gate, and validation. Use when planning, creating, reviewing, executing, or validating planNNN folders, or fixing plan/task format drift."
metadata:
  version: "1.5"
---
# Plan & Task Standards

CatHerder standards for plan folders and task files.

This file is a router. Read only the section for your job, then open the one
reference it points to. You do not need to read everything.

This skill targets **worker agents**. An orchestrator (human or deterministic
driver) directs sequencing and dispatches work; it is not bound by this skill
(see `references/method.md`).

## Posture

CatHerder is deliberately **strict on structure, loose on content and process**.

- **Strict (validator-enforced):** frontmatter keys, status vocabularies,
  required sections, the implementation task-table shape, the open-questions
  gate, and timestamps. These are machine-checked so a human+agent team can
  trust a plan's state without re-reading it.
- **Loose (guidance only):** what you write in each section, how you split work
  into tasks, how granular tasks are, and how you verify. These are judgement
  calls, not rules.

The structure exists to make the *process* lightweight and reliable, not to add
ceremony. It is a standard for the artifacts, not a rigid procedure for how you
must work.

## Canonical Defaults

| Directory | Purpose |
|---|---|
| `.agents/` | Agent-facing assets: agent definitions, instruction files, skills, and MCP configuration |
| `.catherder/` | CatHerder process artifacts: plans and decisions |

Semantic names used after defaults are established:

| Semantic name | Default location |
|---|---|
| agent instructions | `.agents/` |
| plans folder | `.catherder/plans/` |
| decisions folder | `.catherder/decisions/` |
| project CatHerder guidance | `.agents/catherder.project.instructions.md` |

These are canonical defaults. Projects may vary when they have a reason.

## Find Your Job

| You are about to… | Role | Read |
|---|---|---|
| Understand the CatHerder method (always) | any | `references/method.md` |
| Write the prompt and spec (intent) | planner | `references/role-planner.md` |
| Turn a spec into an implementation with tasks | planner | `references/role-planner.md` |
| Execute a task | executor | `references/role-executor.md` |
| Review a spec/implementation, or verify a task | reviewer | `references/role-reviewer.md` |
| Validate or fix file format/drift | any | run the validator (below) |

The full rule set is `references/plan-task-spec-2026-05.md`; open it only when a
role file points you there.

This skill is self-contained. If the project has project CatHerder guidance,
read it and follow its guidance (identity, phase, git workflow, build/test).

## Plan Folder Files

```text
planNNN-short-description/
  planNNN-prompt.md           # original request
  planNNN-spec.md             # intent: what & why
  planNNN-implementation.md   # executable plan + tasks
  planNNN-post-followup.md    # post-completion tracking (optional)
  data/  tasks/  review/
```

Lifecycle: prompt → spec → implementation → (review) → completed → (follow-up).

Plans live in the plans folder. Decisions (ADRs) live in the decisions folder.

## Quick Rules (all roles)

- Status, timestamps, and `type` live in **frontmatter** (`name` is not used).
- **Open-questions gate:** do not create the implementation while any spec open
  question is unresolved.
- A task is `completed` only after verification passed **and** review accepted.

Status values, timestamp format, required sections, and the task-table shape
are in `references/plan-task-spec-2026-05.md`.

## Nested Projects

A nested project (e.g. a Git submodule) is its own CatHerder project with its
own skills/plans — see `references/method.md`.

## Scripts

| Script | Purpose | Usage | Exit Codes |
|---|---|---|---|
| `scripts/validate.sh` | Run the validator via the workspace venv. Report-only. | `bash scripts/validate.sh path/to/planNNN-…` | 0=clean 1=issues 2=usage |
| `scripts/validate.py` | Validate structure, frontmatter, status, timestamps, task table, open-questions gate. Emits JSON. | `python scripts/validate.py path/to/planNNN-…` | 0=clean 1=issues 2=usage/config |

`validate.py` needs `python-frontmatter>=1.1.0`.

## References Map

- Method (behavioural rules): `references/method.md`
- Roles (start here): `references/role-planner.md`,
  `references/role-executor.md`, `references/role-reviewer.md`
- Canonical rules + status lifecycle: `references/plan-task-spec-2026-05.md`
- Templates: `PROMPT_TEMPLATE.md`, `SPEC_TEMPLATE.md`,
  `IMPLEMENTATION_TEMPLATE.md`, `TASK_TEMPLATE.md`, `REVIEW_TEMPLATE.md`,
  `FOLLOWUP_TEMPLATE.md` (all under `references/`)
- Validator rulebook: `references/validation-rules-2026-05.md`
- Trigger/non-trigger prompts: `references/test-prompts-2026-05.md`
- Project adoption: `references/AGENTS_EXAMPLE.md`

## Project Process Rules

This skill is self-contained for the CatHerder method and plan/task format.
Project-specific rules (identity, phase, git workflow, build/test) live in
project CatHerder guidance. If that file is present, read and follow it; it may
add constraints but must not override this skill's method. New projects adopt
the skill via `references/AGENTS_EXAMPLE.md`.

## Scope

- In scope: plan folder layout, prompt/spec/implementation/review/follow-up
  format, lifecycle, frontmatter, status values, timestamps, the
  open-questions gate, structural validation.
- Out of scope: skill file format (see `skill-file-standards`), AGENTS.md
  standards, and project source-code conventions.
