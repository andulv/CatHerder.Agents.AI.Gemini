#!/usr/bin/env bash
# SCRIPT_ID:   validate.sh
# PURPOSE:     Run validate.py against a plan folder using the workspace venv.
# USAGE:       bash scripts/validate.sh <plan-dir>
# ARGS:        plan-dir (required) - path to planNNN-short-description folder
# OUTPUT:      stdout — JSON {target, summary, issues}
# EXIT:        0=clean  1=issues-found  2=usage-error
# DEPS:        python-frontmatter>=1.1.0 (workspace venv preferred; falls back to python3)
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 path/to/planNNN-short-description" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
venv_python="$(cd "$script_dir/../../../.." && pwd)/.venv/bin/python"

if [[ -x "$venv_python" ]]; then
  "$venv_python" "$script_dir/validate.py" "$1"
else
  python3 "$script_dir/validate.py" "$1"
fi
