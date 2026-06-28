---
type: reference
description: "Template for the structured plan prompt file (planNNN-prompt.md)"
snapshot_date: 2026-05-31
sources:
  - references/plan-task-spec-2026-05.md
---
# Prompt Template

Use for `planNNN-prompt.md`. The prompt always starts a plan and captures the
original request as a durable artifact.

```markdown
---
created: YYYY-MM-DDTHH:MM:SS+HH:MM
updated: YYYY-MM-DDTHH:MM:SS+HH:MM
---
# Plan NNN Prompt

## Original prompt

<the request exactly as the sender wrote it, unmodified>

## Interpreted prompt

<same meaning and information, with spelling and formatting fixed; short
factual sentences>
```

Rules:

- `## Original prompt` is verbatim — never edited.
- `## Interpreted prompt` keeps the same meaning; only fixes spelling/formatting.
- A spec is normally created from the prompt automatically (see
  `references/role-planner.md`).
