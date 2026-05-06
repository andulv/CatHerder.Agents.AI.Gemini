---
name: plan-task-standards
description: "Defines CatHerder standards for plan folders and task files (naming, lifecycle stages, frontmatter, status values, timestamps, and validation). Use when creating, reviewing, activating, or validating planNNN folders and task files, or when fixing plan/task format drift."
---
# Plan & Task Standards

Use this skill when working with CatHerder plan folders.

Detailed rules live in `references/`; this file is a compact index + checklist.

## Quick Rules

- Folder name: `planNNN-short-description`.
- Plan files: `planNNN-prompt.md`, `planNNN-draft.md`, `planNNN.md`.
- Plans use frontmatter `type: plan` + `description` (prompt stage frontmatter is optional).
- Task files under `tasks/` are named `taskNNN-NN-short-name.md` and use frontmatter `type: task` + `description`.
- Timestamps in body must use `YYYY-MM-DDTHH:MM:SS+HH:MM`.
- Draft stage is intent-only: no concrete `## Tasks` checklist.
- Active plan is executable: includes task checklist + acceptance criteria.

## Authoring Checklist

- Folder and files use matching `NNN` prefix.
- Status value matches stage (`draft` for draft file; `active/completed/abandoned` for active file).
- Required sections exist for the file stage.
- If active status is `completed`, all task/acceptance checkboxes are checked.
- Separate task files are referenced from the active plan when used.

## Scripts

| Script | Purpose | Usage | Exit Codes | Dependencies / Notes |
|---|---|---|---|---|
| `scripts/validate.sh` | Run `validate.py` via the workspace venv. Report-only; does not modify files. | `bash scripts/validate.sh path/to/planNNN-…` | 0=clean  1=issues-found  2=usage-error | Prefers `../../../../.venv/bin/python`; falls back to `python3`. |
| `scripts/validate.py` | Validate plan folder structure, frontmatter, status, timestamps, and task files. Emits JSON with `error`/`warning` severities. | `python scripts/validate.py path/to/planNNN-…` | 0=clean  1=issues-found  2=usage-or-config-error | Requires `python-frontmatter>=1.1.0`. |

## References Map

- Canonical rules and lifecycle: `references/plan-task-spec-2026-03.md`
- Validator rulebook (checks + severities): `references/validation-rules-2026-03.md`
- Task-file template for per-plan `tasks/`: `references/TASK_TEMPLATE.md`
- Trigger/non-trigger sanity prompts: `references/test-prompts-2026-03.md`

## Related Process Rules

This skill covers plan/task **format and structure**. For behavioural rules that apply during plan authoring and task execution, see `catherder.instructions.md` (copied into each enabled project). Key related concepts:

- **Planning vs execution mode** — when to modify plans vs execute tasks.
- **Prompt precedence** — how to resolve conflicts between instruction sources.
- **Persistence norms** — prefer durable artifacts over ephemeral chat.

## Scope

- In scope: CatHerder plan folder layout, plan/task file format, lifecycle stages, frontmatter keys, status values, timestamps, and structural validation.
- Out of scope: skill file format (see `skill-file-standards`), AGENTS.md standards, and project source-code conventions.
