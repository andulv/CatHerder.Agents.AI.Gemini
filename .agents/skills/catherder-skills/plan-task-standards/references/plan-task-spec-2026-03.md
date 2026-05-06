---
type: reference
description: "Dated consolidated specification for CatHerder plan folders, lifecycle stages, and task files"
snapshot_date: 2026-03-05
sources:
  - docs/catherder/plans-and-tasks.md
  - docs/catherder/file-formats.md
  - docs/catherder/git-workflow.md
  - repo/plans/TEMPLATE.md
  - repo/plans/TEMPLATE-draft.md
  - repo/plans/TEMPLATE-prompt.md
---
# Plan & Task Specification (2026-03)

This reference is the self-contained rule set used by the
`plan-task-standards` skill.

## Plan Folder Structure

Expected structure:

```text
.instructions/plans/
  planNNN-short-description/
    planNNN-prompt.md
    planNNN-draft.md
    planNNN.md
    data/
    tasks/
```

Rules:

- Folder name format: `planNNN-short-description`.
- `NNN` is zero-padded three digits and should be monotonically increasing.
- File names in the folder use matching `planNNN` prefix.

## Lifecycle Stages

- Prompt (`planNNN-prompt.md`): free-form input; frontmatter optional.
- Draft (`planNNN-draft.md`): structured intent; not executable.
- Active (`planNNN.md`): canonical executable plan.

Simple plans may skip prompt stage, but active and draft files must follow
their format rules when present.

## Frontmatter Rules

For plan draft and active files:

- Required keys: `type`, `description`.
- `type` must be `plan`.

For separate task files under `tasks/`:

- Required keys: `type`, `description`.
- `type` must be `task`.

## Status Rules

- Draft file status must be: `draft`.
- Active file status must be one of: `active`, `completed`, `abandoned`.

## Timestamp Rules

Body timestamps (`Created`, `Updated`) must use full ISO 8601 with seconds and
timezone offset:

`YYYY-MM-DDTHH:MM:SS+HH:MM`

Examples:

- `2026-03-04T22:45:00+01:00`
- `2026-03-04T21:45:00+00:00`

`Z` shorthand and date-only values are invalid by convention.

## Required Sections

Draft (`planNNN-draft.md`) should include:

- `## Goal`
- `## Context / Why`
- `## What We Want To Achieve (Outcomes)`
- `## Summary Of Work Needed`
- `## Key Principles / Constraints`
- `## Open Questions`

Active (`planNNN.md`) should include:

- `## Goal`
- `## Context / Why`
- `## Tasks`
- `## Acceptance Criteria`
- `## Notes`

## Draft vs Active Constraint

- Draft files should not contain a concrete `## Tasks` checklist with executable
  `- [ ]` / `- [x]` items.
- Active plan files should carry the executable checklist.

## Separate Task Files

Use separate task files when an inline task is too large/complex.

Task file naming:

- `taskNNN-NN-short-name.md`
- `NNN` must match the parent plan number.
- `NN` is a zero-padded two-digit task number (01, 02, …).

When separate task files exist, active plan should reference them from the
task checklist.
