---
name: skill-file-standards
description: "Defines standards and best practices for authoring Agent Skill folders and SKILL.md files (frontmatter, descriptions/triggers, progressive disclosure, references/scripts/assets, and validation with skills-ref). Use when creating a new skill, reviewing or validating an existing skill, fixing skill discovery issues, or deciding what belongs in SKILL.md vs references/ vs scripts/. Use when user or you want to run skills-ref validation on a skill folder."
---
# Skill File Standards

Use this skill when you need to create, review, or validate an Agent Skill.

This skill is intentionally concise. Detailed (dated) notes live in `references/`.

## Quick Rules (what to remember)

- Frontmatter `description` is routing metadata: include **WHAT** + **WHEN** + user keywords.
- Write `description` in **third person**.- **Skills must be self-contained.** Never reference external files (e.g., `repo/` docs, workflow files, other project paths) as runtime dependencies. Instead, condense all needed knowledge into the skill's own `SKILL.md` and `references/`. External sources are *inputs to skill authoring* — the skill ships its own copy. When sources change, regenerate the skill's references.- Keep `SKILL.md` an index + procedure; push long/volatile material into `references/`.
- Reference files in `references/` MUST use YAML frontmatter (CatHerder convention) with provenance (`snapshot_date`, `sources`) so future humans/agents can re-check upstream.
- Keep references **one hop from `SKILL.md`** (avoid deep chains).
- Use forward slashes in paths (avoid Windows `\\` paths).
- Prefer **defaults + escape hatches**, not menus of options.
- Use scripts for deterministic checks; build a “validate → fix → re-validate” loop.

## Minimal Standard Skill Layout

```
skill-file-standards/
├── SKILL.md
├── references/
└── scripts/
```

Only `SKILL.md` is required by the spec; `references/` and `scripts/` are optional.

## CatHerder Extension: Reference Frontmatter

Upstream spec requires YAML frontmatter only for `SKILL.md`. CatHerder adds a convention:

- Every `references/*.md` MUST start with YAML frontmatter for provenance.
- Required keys: `type: reference`, `description`, `snapshot_date` (YYYY-MM-DD), `sources`.
- Template: `references/REFERENCE_TEMPLATE.md`.

This convention is agent-safe: Markdown frontmatter is either understood or treated as plain text.

## Scripts (provided)

All scripts are optional helpers; they do not change the upstream spec.

| Script | Purpose | Usage | Exit Codes | Dependencies / Notes |
|---|---|---|---|---|
| `scripts/validate.sh` | Validate the skill against the upstream Agent Skills spec (via `skills-ref`). Reports errors; does not modify files. | `bash scripts/validate.sh path/to/skill` | 0=pass  1=errors-found  2=usage-or-config-error  127=skills-ref-not-found | Requires `skills-ref` on PATH. Install notes: `references/skills-ref-2026-03.md`. |
| `scripts/validate-references.sh` | Validate CatHerder reference-frontmatter convention in `references/*.md`. Reports errors; does not modify files. | `bash scripts/validate-references.sh path/to/skill` | 0=pass  1=errors-found  2=usage-error | Uses `bash`, `head`, `awk`, `grep`. Allows `sources: []` and `snapshot_date: YYYY-MM-DD` in templates. |

## Script Header Convention (CatHerder)

Every script in `scripts/` MUST start with a structured comment block immediately after the shebang. This block enables deterministic sub-agents to parse script metadata without executing scripts.

Required keys (in order):

```
# SCRIPT_ID:   <filename>
# PURPOSE:     <one-line description of what it does>
# USAGE:       <executable + args>
# ARGS:        <name (required|optional) - description>  (one line per arg)
# OUTPUT:      <stdout description>
# EXIT:        <code=meaning  code=meaning  …>
# DEPS:        <dependency list; "none" if none>
```

Key rules:
- Align values with spaces (not tabs) for readability.
- `EXIT` must enumerate every exit code the script may return.
- `DEPS` lists what must be installed/on-PATH; not language builtins.

## Authoring Checklist (short)

- `name` matches folder name and passes constraints (lowercase, hyphens, no `--`, etc.).
- `description` includes WHAT + WHEN + keywords; avoid first-person voice.
- `SKILL.md` stays short; anything long or likely to drift goes to `references/`.
- Every referenced file is linked directly from `SKILL.md`.
- Every script in `scripts/` starts with the `# SCRIPT_ID:` header block.
- Validation command exists and is run (prefer `skills-ref validate`).

## Validation

Run both checks when creating/updating a skill:

- Upstream: `skills-ref validate path/to/skill` (or `bash scripts/validate.sh path/to/skill`)
- CatHerder references: `bash scripts/validate-references.sh path/to/skill`

## When to Read Which Reference

- Spec constraints (field lengths, name rules, allowed directories): `references/agent-skills-spec-2026-03.md`
- Authoring heuristics (concision, progressive disclosure, TOC rule): `references/anthropic-best-practices-2026-03.md`
- Validator usage and prompt-generation helpers: `references/skills-ref-2026-03.md`
- Cross-ecosystem placement notes: `references/compatibility-notes-2026-03.md`
- Manual test prompts (trigger / non-trigger sanity checks): `references/test-prompts-2026-03.md`

## Scope

- In scope: skill file format, layout conventions, authoring heuristics, and validation.
- Out of scope: AGENTS.md standards, `.instructions/` plan/task templates, and project-specific workflows (see `agent-file-standards` and other instruction skills).
