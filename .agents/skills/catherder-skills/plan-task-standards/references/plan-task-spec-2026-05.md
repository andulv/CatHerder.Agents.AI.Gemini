---
type: reference
description: "Canonical specification for CatHerder plan folders, lifecycle stages, and task files"
snapshot_date: 2026-06-26
sources:
  - docs/catherder/plans-and-tasks.md
  - docs/catherder/file-formats.md
  - docs/catherder/planning-vs-execution.md
  - docs/catherder/persistence-and-validation-norms.md
---
# Plan & Task Specification (2026-05)

Self-contained rule set for the `plan-task-standards` skill.

## Plan Folder Structure

```text
plans folder/
  planNNN-short-description/
    planNNN-prompt.md            # structured prompt 
    planNNN-spec.md              # intent (was planNNN-draft.md)
    planNNN-implementation.md    # executable plan (was planNNN.md)
    planNNN-post-followup.md     # post-completion tracking (optional)
    data/                        # inputs and generated artifacts
    tasks/                       # one file per task
    review/                      # spec/implementation reviews (optional)
```

Rules:

- Folder name: `planNNN-short-description`; `NNN` is zero-padded, increasing.
- Files in the folder use the matching `planNNN` prefix.

## Lifecycle

prompt → spec → implementation → (review) → completed → (follow-up)

- **Prompt** (`planNNN-prompt.md`): captures the original request.
- **Spec** (`planNNN-spec.md`): defines intent; not executable.
- **Implementation** (`planNNN-implementation.md`): the executable plan + tasks.

Spec and implementation must follow their format when present.

## Frontmatter

Prompt file:

- Keys: `created`, `updated`.

Spec file:

- Keys: `type`, `description`, `status`, `created`, `updated`.
- `type` is `plan-spec`. `status` is `draft` or `ready`.

Implementation file:

- Keys: `type`, `description`, `status`, `created`, `updated`.
- `type` is `plan-implementation`. `status` is `active`, `completed`, or
  `abandoned`.

Task files:

- Keys: `type`, `description`, `status`, `created`, `updated`.
- `type` is `task`.

`name` is not used. Status and timestamps live in frontmatter, not the body.

## Status Values

- Spec: `draft`, `ready`.
- Implementation: `active`, `completed`, `abandoned`.
- Task: `not-started`, `in-progress`, `blocked`, `implemented`, `reviewed`,
  `completed`.

## Task Status Lifecycle

| Status | Meaning | Set by |
|---|---|---|
| `not-started` | Defined, not begun. | planner |
| `in-progress` | Being worked on. | executor |
| `blocked` | Cannot proceed; blocker recorded. | executor |
| `implemented` | Work done, executor verification passed. | executor |
| `reviewed` | Reviewed, not accepted (findings/follow-up pending). | reviewer |
| `completed` | Verification passed and review accepted. | reviewer |

An executor may advance a task up to `implemented`. Only a reviewer sets
`reviewed` or `completed`. The task **file frontmatter `status` is
authoritative**; the implementation task table mirrors it.

## Timestamps

Frontmatter `created`/`updated` (and review `date`) use full ISO 8601 with
seconds and offset: `YYYY-MM-DDTHH:MM:SS+HH:MM`. `Z` shorthand and date-only
are invalid.

## Required Sections

Spec (`planNNN-spec.md`), numbered:

- `## 0. Required Context`
- `## 1. Goal`
- `## 2. Context / Why`
- `## 3. What We Want To Achieve (Outcomes)`
- `## 4. Key Principles / Constraints`
- `## 5. Out of Scope`
- `## 6. Implementation Notes`
- `## 7. Open Questions`

Extra spec sections are allowed, prefixed `A.`, `B.`, … after section 7.

Implementation (`planNNN-implementation.md`):

- `## 0. Required Context`
- `## 1. Tasks`
- `## 2. Task Parallelism`
- `## 3. Acceptance Criteria`

Prompt (`planNNN-prompt.md`), when present:

- `## Original prompt`
- `## Interpreted prompt`

## Open-Questions Gate

In the spec's `## 7. Open Questions`, each question is a numbered item. A
question is **resolved** if it contains `resolved`, `answered`, `closed`, or
`deleted`; **deferred** if it contains `deferred` or `skipped`; otherwise
**unresolved**.

- Spec `status: ready` requires zero unresolved questions.
- If the implementation file exists while the spec has unresolved questions,
  that is an error.
- Deferred questions produce a warning.

## Implementation Task Table

`## 1. Tasks` must use this exact shape so the validator can parse it:

```markdown
## 1. Tasks

Allowed task statuses: not-started, in-progress, blocked, implemented, reviewed, completed.

| Status | Task |
|---|---|
| `not-started` | [Task P080-T01: short objective](tasks/task080-01-short-name.md) |
```

- The legend line `Allowed task statuses: …` must be present.
- Column 1 is the status in backticks (a valid task status).
- Column 2 links to `tasks/taskNNN-NN-...md`. Human IDs use `PNNN-TNN`.
- No checkboxes in this section. Rows off this format are ignored.

The checkbox ban applies to `## 1. Tasks` only. `## 3. Acceptance Criteria`
still uses `- [ ]` / `- [x]` checkboxes.

## Task Files

Naming: `taskNNN-NN-short-name.md`; `NNN` matches the plan, `NN` is
zero-padded (01, 02, …).

Sections:

- `## Required Context`, `## Objective`, `## Scope`, `## Steps`,
  `## Verification`, then a divider and `# Execution`.

The `# Execution` block keeps Executor Notes, Executor Verification, Reviewer
Verification, and Review Notes in the same file, below the divider.

## Review Files

Reviews are written to `review/` so findings and decisions stay traceable, not
ephemeral chat. In `review/`:

- `planNNN-spec-review-<reviewer>.md`,
  `planNNN-implementation-review-<reviewer>.md` — frontmatter `type: review`,
  `reviewer`, `date`, `plan`, `status`.
- `planNNN-spec-review-summary.md`,
  `planNNN-implementation-review-summary.md` — frontmatter
  `type: review-summary`, `date`, `plan`, `sources`.

## Follow-up File (optional)

`planNNN-post-followup.md`: post-completion tracking. No frontmatter required.
Items grouped under `## Blockers`, `## Important`, `## Minor`, numbered, each
naming its source task.

## Completion Semantics

A task is `completed` only when its verification gate passed and review
accepted. Verification must be falsifiable: commands, search checks, or concrete
yes/no checks. Structural (move/rename/delete) tasks need negative checks for
stale paths, imports, entrypoints, and obsolete deployment/config references.

A pre-existing failure may be waived only if listed before execution with
command, expected pattern, scope, and justification. New failures introduced by
the task cannot be waived.

Implementation `status: completed` requires all task rows `completed` and every
acceptance checkbox checked.

## Backward Compatibility (legacy names)

Older plans use `planNNN-draft.md` (spec) and `planNNN.md` (implementation),
`type: plan`, and unnumbered sections. The validator accepts these in **legacy
mode** and emits warnings recommending the new names. Migrate a plan when you
next touch it.
