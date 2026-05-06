---
type: reference
description: "Cross-ecosystem notes: where skill folders live and what tends to differ between clients (Copilot, Claude Code, etc.)"
snapshot_date: 2026-03-04
sources:
  - internal: agent-file-standards skill snapshot
---
# Compatibility Notes — Skill Placement + Variations (2026-03)

These notes are meant to keep skill folders portable.

## Placement conventions (common)

Different clients look in different places. Common patterns include:

- Claude Code: `.claude/skills/<name>/`
- GitHub Copilot: `.github/skills/<name>/` (plus some environments support `.claude/skills/`)
- Cat-Herder: (our own methodology and agentic framework) under `.instructions/skills/<name>`.

## Portability guidelines

- Keep the skill directory structure spec-compliant (SKILL.md + optional scripts/references/assets).
- Avoid client-specific assumptions in the main SKILL.md.
- Put client-specific details into dated `references/` notes.

