#!/usr/bin/env bash
# SCRIPT_ID:   validate-references.sh
# PURPOSE:     Validate CatHerder reference-frontmatter convention in references/*.md.
# USAGE:       bash scripts/validate-references.sh <skill-dir>
# ARGS:        skill-dir (required) - path to the skill folder root
# OUTPUT:      stdout — per-file pass/fail messages; summary on stderr
# EXIT:        0=all-pass  1=errors-found  2=usage-error
# DEPS:        bash, head, awk, grep (standard POSIX tools; no extras required)
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 path/to/skill" >&2
  exit 2
fi

skill_dir="$1"
refs_dir="$skill_dir/references"

if [[ ! -d "$refs_dir" ]]; then
  # No references is OK per upstream spec.
  exit 0
fi

shopt -s nullglob

fail=0
found_any=0

for f in "$refs_dir"/*.md; do
  found_any=1

  # Must start with YAML frontmatter delimiter on the very first line.
  first_line="$(head -n 1 "$f" || true)"
  if [[ "$first_line" != "---" ]]; then
    echo "ERROR: $f: missing YAML frontmatter opening '---' on line 1" >&2
    fail=1
    continue
  fi

  # Find closing delimiter within the first 60 lines (keeps checks fast & predictable).
  fm_end_line="$(head -n 60 "$f" | awk 'NR>1 && $0=="---" { print NR; exit }')"
  if [[ -z "$fm_end_line" ]]; then
    echo "ERROR: $f: missing YAML frontmatter closing '---' within first 60 lines" >&2
    fail=1
    continue
  fi

  frontmatter="$(head -n "$fm_end_line" "$f")"

  # Required keys. These are deliberately simple checks.
  if ! grep -Eq '^type:\s*reference\s*$' <<<"$frontmatter"; then
    echo "ERROR: $f: frontmatter must contain 'type: reference'" >&2
    fail=1
  fi

  if ! grep -Eq '^description:\s*.+$' <<<"$frontmatter"; then
    echo "ERROR: $f: frontmatter must contain 'description: ...'" >&2
    fail=1
  fi

  if ! grep -Eq '^snapshot_date:\s*([0-9]{4}-[0-9]{2}-[0-9]{2}|YYYY-MM-DD)\s*$' <<<"$frontmatter"; then
    echo "ERROR: $f: frontmatter must contain 'snapshot_date: YYYY-MM-DD' (or a concrete date)" >&2
    fail=1
  fi

  if ! grep -Eq '^sources:\s*(\[\s*\])?\s*$' <<<"$frontmatter"; then
    echo "ERROR: $f: frontmatter must contain 'sources:' (optionally 'sources: []')" >&2
    fail=1
  fi

done

if [[ $found_any -eq 0 ]]; then
  # Empty references directory is fine.
  exit 0
fi

if [[ $fail -ne 0 ]]; then
  echo "\nOne or more reference files are missing required frontmatter." >&2
  echo "Start from: $skill_dir/references/REFERENCE_TEMPLATE.md" >&2
  exit 1
fi

exit 0
