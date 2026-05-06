#!/usr/bin/env bash
# SCRIPT_ID:   validate.sh
# PURPOSE:     Validate a skill folder against the upstream Agent Skills spec via skills-ref.
# USAGE:       bash scripts/validate.sh <skill-dir>
# ARGS:        skill-dir (required) - path to the skill folder root
# OUTPUT:      stdout — validation results from skills-ref
# EXIT:        0=pass  1=errors-found  2=usage-or-config-error  127=skills-ref-not-found
# DEPS:        skills-ref (on PATH or at ../../../../.venv/bin/skills-ref); see references/skills-ref-2026-03.md
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 path/to/skill" >&2
  exit 2
fi

skill_dir="$1"

if [[ ! -f "$skill_dir/SKILL.md" ]]; then
  echo "No SKILL.md found at: $skill_dir" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
install_ref="$script_dir/../references/skills-ref-2026-03.md"
venv_skills_ref="$script_dir/../../../../.venv/bin/skills-ref"

skills_ref_cmd=""
if command -v skills-ref >/dev/null 2>&1; then
  skills_ref_cmd="skills-ref"
elif [[ -x "$venv_skills_ref" ]]; then
  skills_ref_cmd="$venv_skills_ref"
fi

if [[ -z "$skills_ref_cmd" ]]; then
  echo "skills-ref not found on PATH." >&2
  echo "Also checked: $venv_skills_ref" >&2
  echo "Install it (see: $install_ref) then re-run:" >&2
  echo "  skills-ref validate \"$skill_dir\"" >&2
  exit 127
fi

"$skills_ref_cmd" validate "$skill_dir"
