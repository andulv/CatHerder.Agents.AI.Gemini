---
type: reference
description: "Validation checks and severity model for plan-task-standards validator"
snapshot_date: 2026-03-05
sources:
  - references/plan-task-spec-2026-03.md
  - docs/catherder/plans-and-tasks.md
  - docs/catherder/file-formats.md
---
# Validation Rules (2026-03)

This defines what `scripts/validate.py` checks and how findings are classified.

## Severity Model

- `error`: must fix; validator exits non-zero.
- `warning`: recommendation/style/gap; validator exits zero if only warnings.

## Structural Checks

Errors:

- Invalid plan folder name format.
- File name prefix mismatch (`planNNN` mismatch).
- Missing required frontmatter in draft/active/task files.
- Invalid `type` (`plan`/`task`) for applicable files.
- Invalid status for draft/active files.
- Invalid timestamp format for `Created`/`Updated` lines.
- Invalid task filename format.

Warnings:

- Optional lifecycle file missing (for example prompt stage).
- Unknown extra markdown file in plan root.
- `tasks/` contains no task files but active plan references task files.

## Content Checks

Errors:

- Missing required sections for draft/active files.
- Draft file contains concrete `## Tasks` checklist.
- Active file with status `completed` has unchecked task/acceptance checkbox.

Warnings:

- Active plan uses separate task files but no explicit references are found.
- Active file status `abandoned` has no apparent rationale in Notes.

## Output Contract

Validator returns JSON with:

- `target`
- `summary` (`errors`, `warnings`, `files_checked`)
- `issues[]` with `severity`, `code`, `path`, `message`

Exit codes:

- `0`: no errors (warnings allowed)
- `1`: one or more errors
- `2`: usage/input path issues
