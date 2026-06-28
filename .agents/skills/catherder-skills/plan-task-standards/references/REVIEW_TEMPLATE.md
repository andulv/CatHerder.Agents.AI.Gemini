---
type: reference
description: "Templates for review files under a plan's review/ folder"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Review Templates

Reviews are durable artifacts: write them to the plan's `review/` folder (not
chat) so findings and decisions stay traceable. Reviews exist for the spec and/or the implementation.

## Per-reviewer review

File: `review/planNNN-spec-review-<reviewer>.md` or
`review/planNNN-implementation-review-<reviewer>.md`.

```markdown
---
type: review
reviewer: <name-or-model>
date: YYYY-MM-DDTHH:MM:SS+HH:MM
plan: NNN
status: <approved | changes-requested | rejected>
---
# Plan NNN <Spec|Implementation> Review — <reviewer>

## Summary

Overall judgement in a few sentences.

## Findings

1. <finding> — severity, location, suggested fix.

---

Everything above is the review. Below is the optional response/iteration thread —
append one attributed entry per turn; update frontmatter `status` when it
converges (e.g. → approved).

# Response

## Author Response
By: <name-or-model> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

Per finding: disposition (address / won't-address / partial) + action taken.

## Reviewer Re-review (second pass)
By: <name-or-model> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

Re-checked the addressed items; verdict.
```

## Review summary

File: `review/planNNN-spec-review-summary.md` or
`review/planNNN-implementation-review-summary.md`. Synthesizes the per-reviewer
files.

```markdown
---
type: review-summary
date: YYYY-MM-DDTHH:MM:SS+HH:MM
plan: NNN
sources:
  - review/planNNN-spec-review-<reviewer1>.md
  - review/planNNN-spec-review-<reviewer2>.md
---
# Plan NNN <Spec|Implementation> Review Summary

## Overall Read

## Consensus

## Disagreement

## Carry-forward Items
```
