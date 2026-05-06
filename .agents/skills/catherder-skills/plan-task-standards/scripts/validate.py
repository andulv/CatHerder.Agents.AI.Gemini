#!/usr/bin/env python3
# SCRIPT_ID:   validate.py
# PURPOSE:     Validate CatHerder plan folder structure, frontmatter, lifecycle, and task files.
# USAGE:       python scripts/validate.py <plan-dir>
# ARGS:        plan-dir (required) - path to planNNN-short-description folder
# OUTPUT:      stdout — JSON {target, summary:{errors,warnings,files_checked}, issues:[{file,severity,code,message}]}
# EXIT:        0=clean  1=issues-found  2=usage-or-config-error
# DEPS:        python-frontmatter>=1.1.0  (pip install python-frontmatter)
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any

try:
    import frontmatter
except ImportError as exc:
    print(
        "{\"error\": \"Missing dependency: python-frontmatter. Install with: pip install python-frontmatter\"}",
        file=sys.stderr,
    )
    raise SystemExit(2) from exc


ISO_TS_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$")
FOLDER_RE = re.compile(r"^plan(?P<num>\d{3})-[a-z0-9]+(?:-[a-z0-9]+)*$")
TASK_FILE_RE = re.compile(r"^task(?P<num>\d{3})-(?P<tasknum>\d{2})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$")
PLAN_DRAFT_RE = re.compile(r"^plan(?P<num>\d{3})-draft\.md$")
PLAN_PROMPT_RE = re.compile(r"^plan(?P<num>\d{3})-prompt\.md$")
PLAN_ACTIVE_RE = re.compile(r"^plan(?P<num>\d{3})\.md$")


@dataclass
class Finding:
    severity: str
    code: str
    path: str
    message: str


def make_finding(severity: str, code: str, path: Path, message: str) -> Finding:
    return Finding(severity=severity, code=code, path=str(path), message=message)


def load_markdown(path: Path) -> tuple[dict[str, Any], str]:
    post = frontmatter.load(path)
    return dict(post.metadata), str(post.content)


def extract_field_value(content: str, field_name: str) -> str | None:
    pattern = re.compile(rf"^\*\*{re.escape(field_name)}:\*\*\s*(.+?)\s*$", re.MULTILINE)
    match = pattern.search(content)
    return match.group(1).strip() if match else None


def has_section(content: str, heading: str) -> bool:
    return re.search(rf"^##\s+{re.escape(heading)}\s*$", content, re.MULTILINE) is not None


def count_unchecked_boxes(content: str) -> int:
    return len(re.findall(r"^\s*- \[ \]", content, re.MULTILINE))


def contains_task_checklist_in_tasks_section(content: str) -> bool:
    match = re.search(r"^##\s+Tasks\s*$", content, re.MULTILINE)
    if not match:
        return False
    start = match.end()
    next_heading = re.search(r"^##\s+", content[start:], re.MULTILINE)
    end = start + next_heading.start() if next_heading else len(content)
    section = content[start:end]
    return bool(re.search(r"^\s*- \[[ xX]\]", section, re.MULTILINE))


def contains_task_file_reference(content: str) -> bool:
    return bool(re.search(r"tasks/task[0-9]{3}-[0-9]{2}-[a-z0-9-]+\.md", content))


def validate_timestamp(field_name: str, value: str | None, path: Path, findings: list[Finding]) -> None:
    if value is None:
        findings.append(make_finding("error", "missing_timestamp", path, f"Missing `{field_name}` line."))
        return
    cleaned = value.strip("`")
    if not ISO_TS_RE.match(cleaned):
        findings.append(
            make_finding(
                "error",
                "invalid_timestamp",
                path,
                f"`{field_name}` must match YYYY-MM-DDTHH:MM:SS+HH:MM.",
            )
        )
        return
    try:
        datetime.fromisoformat(cleaned)
    except ValueError:
        findings.append(make_finding("error", "invalid_timestamp", path, f"`{field_name}` is not parseable ISO timestamp."))


def validate_plan_file(
    path: Path,
    expected_num: str,
    stage: str,
    findings: list[Finding],
) -> tuple[dict[str, Any], str] | None:
    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse markdown/frontmatter: {exc}"))
        return None

    if metadata.get("type") != "plan":
        findings.append(make_finding("error", "invalid_type", path, "Frontmatter `type` must be `plan`."))
    if not metadata.get("description"):
        findings.append(make_finding("error", "missing_description", path, "Frontmatter `description` is required."))

    status = extract_field_value(content, "Status")
    if status is None:
        findings.append(make_finding("error", "missing_status", path, "Missing `**Status:**` line."))
    else:
        status_clean = status.strip("`")
        if stage == "draft" and status_clean != "draft":
            findings.append(make_finding("error", "invalid_status", path, "Draft plan status must be `draft`."))
        if stage == "active" and status_clean not in {"active", "completed", "abandoned"}:
            findings.append(
                make_finding("error", "invalid_status", path, "Active plan status must be `active`, `completed`, or `abandoned`."),
            )

    validate_timestamp("Created", extract_field_value(content, "Created"), path, findings)
    validate_timestamp("Updated", extract_field_value(content, "Updated"), path, findings)

    if stage == "draft":
        for section in [
            "Goal",
            "Context / Why",
            "What We Want To Achieve (Outcomes)",
            "Summary Of Work Needed",
            "Key Principles / Constraints",
            "Open Questions",
        ]:
            if not has_section(content, section):
                findings.append(make_finding("error", "missing_section", path, f"Draft missing section: `## {section}`."))
        if contains_task_checklist_in_tasks_section(content):
            findings.append(make_finding("error", "draft_has_task_checklist", path, "Draft must not contain a concrete `## Tasks` checklist."))

    if stage == "active":
        for section in ["Goal", "Context / Why", "Tasks", "Acceptance Criteria", "Notes"]:
            if not has_section(content, section):
                findings.append(make_finding("error", "missing_section", path, f"Active plan missing section: `## {section}`."))

        status_clean = (extract_field_value(content, "Status") or "").strip("`")
        if status_clean == "completed" and count_unchecked_boxes(content) > 0:
            findings.append(
                make_finding("error", "completed_has_unchecked", path, "Plan status is `completed` but contains unchecked checklist items."),
            )
        if status_clean == "abandoned":
            notes_has_reason = bool(re.search(r"abandon|dropped|reason|why", content, re.IGNORECASE))
            if not notes_has_reason:
                findings.append(make_finding("warning", "abandoned_without_reason", path, "Abandoned plan should document rationale in Notes."))

    file_num = expected_num
    if f"plan{file_num}" not in path.name:
        findings.append(make_finding("error", "file_prefix_mismatch", path, "File prefix does not match folder plan number."))

    return metadata, content


def validate_task_file(path: Path, expected_num: str, findings: list[Finding]) -> None:
    match = TASK_FILE_RE.match(path.name)
    if not match:
        findings.append(
            make_finding(
                "error",
                "invalid_task_filename",
                path,
                "Task filename must match taskNNN-NN-short-name.md (e.g., task002-01-my-task.md).",
            )
        )
        return
    if match.group("num") != expected_num:
        findings.append(make_finding("error", "task_prefix_mismatch", path, "Task filename NNN must match parent plan number."))

    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse markdown/frontmatter: {exc}"))
        return

    if metadata.get("type") != "task":
        findings.append(make_finding("error", "invalid_type", path, "Task file frontmatter `type` must be `task`."))
    if not metadata.get("description"):
        findings.append(make_finding("error", "missing_description", path, "Task file frontmatter `description` is required."))

    validate_timestamp("Created", extract_field_value(content, "Created"), path, findings)
    validate_timestamp("Updated", extract_field_value(content, "Updated"), path, findings)


def validate_plan_folder(plan_dir: Path) -> dict[str, Any]:
    findings: list[Finding] = []
    files_checked = 0

    folder_match = FOLDER_RE.match(plan_dir.name)
    if not folder_match:
        findings.append(
            make_finding(
                "error",
                "invalid_folder_name",
                plan_dir,
                "Plan folder must match `planNNN-short-description`.",
            )
        )
        expected_num = None
    else:
        expected_num = folder_match.group("num")

    prompt_file = None
    draft_file = None
    active_file = None

    for child in plan_dir.iterdir():
        if child.is_file() and child.suffix == ".md":
            if PLAN_PROMPT_RE.match(child.name):
                prompt_file = child
            elif PLAN_DRAFT_RE.match(child.name):
                draft_file = child
            elif PLAN_ACTIVE_RE.match(child.name):
                active_file = child
            else:
                findings.append(make_finding("warning", "unknown_markdown_file", child, "Unrecognized markdown file in plan root."))

    if expected_num:
        if not prompt_file:
            findings.append(make_finding("warning", "missing_prompt_file", plan_dir, f"Optional file missing: `plan{expected_num}-prompt.md`."))
        if not draft_file:
            findings.append(make_finding("warning", "missing_draft_file", plan_dir, f"Expected draft file missing: `plan{expected_num}-draft.md`."))
        if not active_file:
            findings.append(make_finding("warning", "missing_active_file", plan_dir, f"Expected active file missing: `plan{expected_num}.md`."))

    active_content = ""
    if expected_num and draft_file:
        files_checked += 1
        validate_plan_file(draft_file, expected_num, "draft", findings)
    if expected_num and active_file:
        files_checked += 1
        result = validate_plan_file(active_file, expected_num, "active", findings)
        if result is not None:
            _, active_content = result

    tasks_dir = plan_dir / "tasks"
    task_files: list[Path] = []
    if tasks_dir.exists() and tasks_dir.is_dir() and expected_num:
        task_files = sorted([p for p in tasks_dir.iterdir() if p.is_file() and p.suffix == ".md"])
        for task_file in task_files:
            files_checked += 1
            validate_task_file(task_file, expected_num, findings)

    if task_files and active_content and not contains_task_file_reference(active_content):
        findings.append(
            make_finding(
                "warning",
                "task_files_not_referenced",
                active_file if active_file else plan_dir,
                "Task files exist but active plan does not appear to reference `tasks/taskNNN-NN-...md`.",
            )
        )

    errors = [f for f in findings if f.severity == "error"]
    warnings = [f for f in findings if f.severity == "warning"]

    return {
        "target": str(plan_dir),
        "summary": {
            "errors": len(errors),
            "warnings": len(warnings),
            "files_checked": files_checked,
        },
        "issues": [f.__dict__ for f in findings],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate CatHerder plan folder structure and semantics.")
    parser.add_argument("plan_folder", help="Path to planNNN-short-description folder")
    args = parser.parse_args()

    plan_dir = Path(args.plan_folder).resolve()
    if not plan_dir.exists() or not plan_dir.is_dir():
        print(json.dumps({"error": f"Invalid plan folder: {plan_dir}"}, indent=2))
        return 2

    result = validate_plan_folder(plan_dir)
    print(json.dumps(result, indent=2))
    return 1 if result["summary"]["errors"] > 0 else 0


if __name__ == "__main__":
    raise SystemExit(main())
