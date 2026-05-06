---
type: reference
description: "Dated notes paraphrasing the Agent Skills specification (field constraints, structure, and validation guidance)"
snapshot_date: 2026-03-04
sources:
  - https://agentskills.io/specification
---
# Agent Skills Specification — Notes (2026-03)

These are paraphrased notes from the Agent Skills specification as retrieved on 2026-03-04. Re-check the source before relying on exact limits.

## Directory structure

- A skill is a directory containing at minimum `SKILL.md`.
- Optional support directories commonly include:
  - `scripts/` — executable helpers
  - `references/` — documentation to be read on demand
  - `assets/` — static resources (images/data/templates)

## `SKILL.md` format

- `SKILL.md` must begin with YAML frontmatter followed by Markdown.

### Required fields

- `name`
  - 1–64 chars
  - lowercase alphanumeric + hyphen only
  - must not start or end with `-`
  - must not contain consecutive `--`
  - must match the parent directory name

- `description`
  - 1–1024 chars
  - should include WHAT the skill does and WHEN to use it
  - include keywords that help routing/selection

### Optional fields (selected)

- `license`: short identifier or name of bundled license file
- `compatibility`: short environment requirements (most skills don’t need it)
- `metadata`: string→string mapping for extra properties
- `allowed-tools`: space-delimited list of pre-approved tools (experimental; client support varies)

## Body content

- The Markdown body has no strict schema. Recommended: steps, examples, edge cases.
- Once a skill triggers, the full SKILL.md body is loaded; keep it short and offload long material to references.

## Progressive disclosure

- Metadata is loaded for all skills, then SKILL.md on activation, then resources as needed.
- Keep SKILL.md under ~500 lines (rule of thumb) and move detail into separate files.

## File references

- Reference paths should be relative from the skill root.
- Keep references one hop from SKILL.md; avoid deep chains.

## Validation

- The spec points to `skills-ref` as a reference implementation for validation.
- Typical usage: `skills-ref validate ./my-skill`
