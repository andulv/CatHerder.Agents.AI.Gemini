---
type: reference
description: "Dated notes paraphrasing Anthropic/Claude Agent Skill authoring best practices (concision, information architecture, reference depth)"
snapshot_date: 2026-03-04
sources:
  - https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices
---
# Anthropic Skill Authoring Best Practices — Notes (2026-03)

These are paraphrased notes from Anthropic’s skill authoring best-practices page as retrieved on 2026-03-04.

## Concision and token economics

- Treat context window as shared resource: once SKILL.md is loaded, every token competes with conversation and other context.
- Default assumption: the model already knows general concepts; only include what’s non-obvious or easy to miss.

## Degrees of freedom

Match instruction specificity to task fragility:

- High freedom: heuristics / general steps when multiple approaches are valid.
- Medium freedom: templates/pseudocode where some adaptation is expected.
- Low freedom: exact commands/scripts for fragile workflows where consistency matters.

## Naming and descriptions

- `name` should follow spec constraints; consistent naming patterns help maintainability.
- `description` is used for discovery/routing; include WHAT + WHEN + key terms.
- Write descriptions in third person (routing metadata injected into system context).

## Progressive disclosure patterns

- SKILL.md should behave like an index/table of contents pointing to deeper material.
- Keep SKILL.md under ~500 lines (rule of thumb). Split content into separate files when it grows.

### Avoid deep reference chains

- Avoid referencing file A from SKILL.md, then file B from file A, etc.
- Models may partially read nested references (e.g., preview with `head`) and miss the real content.
- Prefer “one hop from SKILL.md”: every referenced file should be linked directly from SKILL.md.

### Long references should start with a TOC

- If a reference file is long (rule of thumb: >100 lines), add a small table of contents near the top.

## Anti-patterns called out

- Windows-style backslashes in paths (prefer forward slashes universally).
- Providing too many alternative approaches without a default.
- Time-sensitive instructions in the main flow; quarantine legacy notes separately.

## Feedback loops

- Encode validator loops: validate → fix → re-validate.
- Use checklists for complex workflows.
