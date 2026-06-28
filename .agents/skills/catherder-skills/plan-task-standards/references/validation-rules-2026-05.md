---
type: reference
description: "Validation checks and severity model for the plan-task-standards validator"
snapshot_date: 2026-06-26
sources:
  - references/plan-task-spec-2026-05.md
---
# Validation Rules (2026-05)

What `scripts/validate.py` checks and how findings are classified.

## Severity Model

- `error`: must fix; validator exits non-zero.
- `warning`: recommendation/gap; validator exits zero if only warnings.

## Modes

The validator picks a mode per plan folder:

- **current** — `planNNN-spec.md` and/or `planNNN-implementation.md` present.
  New rules apply.
- **legacy** — only `planNNN-draft.md` and/or `planNNN.md` present. Old rules
  apply leniently: plan-section gaps and task structure are warnings, not
  errors, so legacy plans exit `0`. The folder gets a `legacy_plan_format`
  warning. Task files are checked old-style (type + description + body
  timestamps only).

Section matching accepts an optional `N. ` numeric prefix, so `## 1. Goal` and
`## Goal` both satisfy the `Goal` requirement.

## Structural Checks

Errors:

- Invalid plan folder name (`invalid_folder_name`).
- File/task prefix mismatch (`file_prefix_mismatch`, `task_prefix_mismatch`).
- Missing required frontmatter (`missing_frontmatter`).
- Invalid `type` for the file kind (`invalid_type`).
- Invalid spec/implementation/task status (`invalid_status`,
  `invalid_task_status`).
- Invalid `created`/`updated`/`date` timestamp (`invalid_timestamp`).
- Invalid task filename (`invalid_task_filename`).
- Implementation `## 1. Tasks` uses checkboxes (`task_checkboxes_not_allowed`).
- Missing allowed-status legend line in tasks (`missing_task_status_legend`).
- Task table references a missing file (`missing_task_file`).
- Table status differs from task frontmatter (`task_status_mismatch`).

Warnings:

- Legacy plan format / deprecated filename (`legacy_plan_format`,
  `deprecated_filename`).
- Expected lifecycle file missing (`missing_spec_file`,
  `missing_implementation_file`, `missing_prompt_file`).
- Unknown extra markdown file in plan root (`unknown_markdown_file`).
- Task files exist but implementation does not reference them
  (`task_files_not_referenced`).

## Content Checks

Errors:

- Missing required sections in spec/implementation/task files
  (`missing_section`), including `# Execution` in tasks. In **legacy** mode,
  missing plan sections are warnings instead.
- Spec `status: ready` with unresolved open questions
  (`ready_with_open_questions`).
- Implementation exists while spec has unresolved open questions
  (`open_questions_block_implementation`).
- Implementation `status: completed` with unchecked acceptance criteria
  (`completed_has_unchecked`) or non-`completed` task rows
  (`completed_has_incomplete_tasks`).

Warnings:

- Spec has deferred open questions (`open_questions_deferred`).
- Implementation `status: abandoned` without rationale (`abandoned_without_reason`).
- Prompt file missing `## Original prompt` / `## Interpreted prompt`
  (`prompt_missing_section`).
- Follow-up file with no priority section (`followup_missing_section`).

## Review Files

When a `review/` file is present:

- Errors: invalid `type` (`invalid_type`), missing required review frontmatter
  (`missing_frontmatter`), invalid `date` timestamp (`invalid_timestamp`).

## Output Contract

JSON with `target`, `summary` (`errors`, `warnings`, `files_checked`), and
`issues[]` of `{severity, code, path, message}`.

Exit codes: `0` no errors (warnings allowed), `1` one or more errors, `2`
usage/input error.
