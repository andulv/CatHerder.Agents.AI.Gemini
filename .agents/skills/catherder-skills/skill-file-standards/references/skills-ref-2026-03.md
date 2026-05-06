---
type: reference
description: "Dated notes on skills-ref (reference validator) installation and usage"
snapshot_date: 2026-03-04
sources:
  - https://github.com/agentskills/agentskills/tree/main/skills-ref
---
# skills-ref — Notes (2026-03)

These are paraphrased notes from the `skills-ref` README as retrieved on 2026-03-04.

## What it is

- `skills-ref` is a reference library + CLI for working with the Agent Skills format.
- The README explicitly frames it as a demonstration/reference implementation (not necessarily “production-ready”).

## Installation (typical)

- Create/activate a Python virtualenv, then install the package.
- Alternative: use `uv` if you already use it.

(Exact steps vary; consult the upstream README.)

## Installation (tested in this repo)

Tested on 2026-03-04 by installing directly from the upstream GitHub repo’s `skills-ref` subdirectory.

From the repo root:

```bash
python -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install "git+https://github.com/agentskills/agentskills.git#subdirectory=skills-ref"
```

Then validate:

```bash
skills-ref validate repo/skills/skill-file-standards
```

## CLI commands (useful subset)

- Validate a skill directory:
  - `skills-ref validate path/to/skill`

- Read a skill’s properties as JSON:
  - `skills-ref read-properties path/to/skill`

- Generate an `<available_skills>` prompt block from one or more skills:
  - `skills-ref to-prompt path/to/skill-a path/to/skill-b`

## Python API (high level)

- Provides functions mirroring CLI functionality: validate, read-properties, to-prompt.

## Local wrapper scripts

This skill includes an optional wrapper:
- `scripts/validate.sh path/to/skill`

It still requires `skills-ref` to be installed on PATH; it just standardizes invocation and messaging.
