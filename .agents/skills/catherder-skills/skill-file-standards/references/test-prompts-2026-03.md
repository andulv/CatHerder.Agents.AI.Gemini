---
type: reference
description: "Small manual test set for skill discovery and usefulness checks"
snapshot_date: 2026-03-04
sources: []
---
# Test Prompts — skill-file-standards (2026-03)

Use these prompts to sanity-check that this skill:
- triggers when it should,
- stays concise,
- points to the right reference notes,
- and produces actionable guidance.

## Should Trigger

1) Create / scaffold
- “Create a new Agent Skill for <X>. What folder structure and SKILL.md frontmatter should I use?”

2) Fix discovery
- “My skill isn’t being discovered. How should I change the `description` to improve triggering?”

3) Validate / lint
- “Validate this skill folder. Check name/folder match, frontmatter constraints, and reference layout.”

4) Information architecture
- “What belongs in SKILL.md vs `references/` vs `scripts/` for a skill?”

5) Reference depth
- “My skill has references that link to other references. Is that OK?”

## Should Not Trigger

1) General YAML questions
- “How do I write YAML frontmatter in Markdown?”

2) Non-skill repo docs
- “How should we structure our `.instructions/` plans and tasks?”

3) Pure coding questions
- “Write a Python function to parse YAML.”

## What To Look For In Answers

- Mentions that `description` is routing metadata (WHAT + WHEN + keywords, third person).
- Advises progressive disclosure and one-hop references.
- Recommends defaults + escape hatches (not option dumps).
- Suggests `skills-ref validate` as the validator loop.
- Keeps volatile details in references and doesn’t duplicate long spec content in SKILL.md.
