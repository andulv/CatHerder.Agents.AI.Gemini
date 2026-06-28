---
type: reference
description: "Template for the post-completion follow-up file (planNNN-post-followup.md)"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Follow-up Template

Use for `planNNN-post-followup.md`. Optional working document that tracks
deferred issues, unmet acceptance criteria, and known gaps after a plan is
completed — instead of leaving them in chat. No frontmatter required.

```markdown
# Plan NNN Follow-up

## Blockers

1. <description> — complexity: <low|med|high>; fix: <open|in-progress|done>; source: task PNNN-TNN.

## Important

1. <description> — complexity: …; fix: …; source: …

## Minor

1. <description> — complexity: …; fix: …; source: …
```

Rules:

- Group items by priority: `## Blockers`, `## Important`, `## Minor`.
- Number items within each group for stable reference.
- Each item names its source task where applicable.
