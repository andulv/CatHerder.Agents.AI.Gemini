#!/usr/bin/env python3
# SCRIPT_ID:   validate.py
# PURPOSE:     Validate CatHerder plan folder structure, frontmatter, lifecycle, and task files.
# USAGE:       python scripts/validate.py <plan-dir>
# ARGS:        plan-dir (required) - path to planNNN-short-description folder
# OUTPUT:      stdout — JSON {target, summary:{errors,warnings,files_checked}, issues:[{severity,code,path,message}]}
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
        '{"error": "Missing dependency: python-frontmatter. Install with: pip install python-frontmatter"}',
        file=sys.stderr,
    )
    raise SystemExit(2) from exc


ISO_TS_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$")
FOLDER_RE = re.compile(r"^plan(?P<num>\d{3})-[a-z0-9]+(?:-[a-z0-9]+)*$")
TASK_FILE_RE = re.compile(r"^task(?P<num>\d{3})-(?P<tasknum>\d{2})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$")

PROMPT_RE = re.compile(r"^plan(?P<num>\d{3})-prompt\.md$")
SPEC_RE = re.compile(r"^plan(?P<num>\d{3})-spec\.md$")
IMPL_RE = re.compile(r"^plan(?P<num>\d{3})-implementation\.md$")
FOLLOWUP_RE = re.compile(r"^plan(?P<num>\d{3})-post-followup\.md$")
# Legacy names
DRAFT_RE = re.compile(r"^plan(?P<num>\d{3})-draft\.md$")
ACTIVE_RE = re.compile(r"^plan(?P<num>\d{3})\.md$")

SPEC_STATUSES = {"draft", "ready"}
IMPL_STATUSES = {"active", "completed", "abandoned"}
TASK_STATUSES = {"not-started", "in-progress", "blocked", "implemented", "reviewed", "completed"}
REVIEW_TYPES = {"review", "review-summary"}
TASK_STATUS_LINE = "Allowed task statuses:"

RESOLVED_MARKERS = ("resolved", "answered", "closed", "deleted")
DEFERRED_MARKERS = ("deferred", "skipped")


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


def metadata_text(metadata: dict[str, Any], key: str) -> str | None:
    value = metadata.get(key)
    if isinstance(value, str):
        return value.strip()
    if isinstance(value, datetime):
        return value.isoformat()
    if value is not None:
        return str(value)
    return None


def extract_field_value(content: str, field_name: str) -> str | None:
    pattern = re.compile(rf"^\*\*{re.escape(field_name)}:\*\*\s*(.+?)\s*$", re.MULTILINE)
    match = pattern.search(content)
    return match.group(1).strip() if match else None


def _heading_pattern(heading: str) -> re.Pattern[str]:
    # Match `## Heading` or `## 1. Heading` or `## A. Heading`.
    return re.compile(rf"^##\s+(?:[0-9A-Z]+\.\s+)?{re.escape(heading)}\s*$", re.MULTILINE)


def has_section(content: str, heading: str) -> bool:
    return _heading_pattern(heading).search(content) is not None


def get_section(content: str, heading: str) -> str:
    match = _heading_pattern(heading).search(content)
    if not match:
        return ""
    start = match.end()
    next_heading = re.search(r"^##\s+", content[start:], re.MULTILINE)
    end = start + next_heading.start() if next_heading else len(content)
    return content[start:end]


def count_unchecked_boxes(content: str) -> int:
    return len(re.findall(r"^\s*- \[ \]", content, re.MULTILINE))


def tasks_section_has_checkboxes(content: str) -> bool:
    return bool(re.search(r"^\s*- \[[ xX]\]", get_section(content, "Tasks"), re.MULTILINE))


def contains_task_file_reference(content: str) -> bool:
    return bool(re.search(r"tasks/task[0-9]{3}-[0-9]{2}-[a-z0-9-]+\.md", content))


def extract_task_table_statuses(content: str) -> dict[str, str]:
    section = get_section(content, "Tasks")
    result: dict[str, str] = {}
    row_re = re.compile(
        r"^\|\s*`(?P<status>[^`]+)`\s*\|\s*\[[^\]]+\]\((?P<link>tasks/task[0-9]{3}-[0-9]{2}-[a-z0-9-]+\.md)\)\s*\|",
        re.MULTILINE,
    )
    for match in row_re.finditer(section):
        result[match.group("link")] = match.group("status")
    return result


def classify_open_questions(content: str) -> tuple[int, int]:
    """Return (unresolved, deferred) counts from the Open Questions section."""
    section = get_section(content, "Open Questions")
    items = re.findall(r"^\s*\d+\.\s+(.*)$", section, re.MULTILINE)
    unresolved = 0
    deferred = 0
    for item in items:
        low = item.lower()
        if any(m in low for m in RESOLVED_MARKERS):
            continue
        if any(m in low for m in DEFERRED_MARKERS):
            deferred += 1
            continue
        unresolved += 1
    return unresolved, deferred


def validate_required_metadata(
    path: Path, metadata: dict[str, Any], keys: list[str], findings: list[Finding]
) -> None:
    for key in keys:
        if metadata_text(metadata, key) is None:
            findings.append(
                make_finding("error", "missing_frontmatter", path, f"Frontmatter `{key}` is required.")
            )


def validate_timestamp(field_name: str, value: str | None, path: Path, findings: list[Finding]) -> None:
    if value is None:
        return
    cleaned = value.strip().strip("`")
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
        findings.append(
            make_finding("error", "invalid_timestamp", path, f"`{field_name}` is not a parseable ISO timestamp.")
        )


def check_sections(
    path: Path, content: str, sections: list[str], kind: str, findings: list[Finding], severity: str = "error"
) -> None:
    for section in sections:
        if not has_section(content, section):
            findings.append(
                make_finding(severity, "missing_section", path, f"{kind} missing section: `## {section}`.")
            )


def check_prefix(path: Path, expected_num: str, findings: list[Finding]) -> None:
    if f"plan{expected_num}" not in path.name:
        findings.append(
            make_finding("error", "file_prefix_mismatch", path, "File prefix does not match folder plan number.")
        )


# ---------- Current-mode plan files ----------

SPEC_SECTIONS = [
    "Required Context",
    "Goal",
    "Context / Why",
    "What We Want To Achieve (Outcomes)",
    "Key Principles / Constraints",
    "Out of Scope",
    "Implementation Notes",
    "Open Questions",
]
IMPL_SECTIONS = ["Required Context", "Tasks", "Task Parallelism", "Acceptance Criteria"]


def validate_spec_file(path: Path, expected_num: str, findings: list[Finding]) -> tuple[int, int] | None:
    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return None

    validate_required_metadata(path, metadata, ["type", "description", "status", "created", "updated"], findings)
    if metadata.get("type") != "plan-spec":
        findings.append(make_finding("error", "invalid_type", path, "Spec frontmatter `type` must be `plan-spec`."))

    status = metadata_text(metadata, "status")
    if status is not None and status not in SPEC_STATUSES:
        findings.append(make_finding("error", "invalid_status", path, "Spec status must be `draft` or `ready`."))

    validate_timestamp("created", metadata_text(metadata, "created"), path, findings)
    validate_timestamp("updated", metadata_text(metadata, "updated"), path, findings)
    check_sections(path, content, SPEC_SECTIONS, "Spec", findings)
    check_prefix(path, expected_num, findings)

    unresolved, deferred = classify_open_questions(content)
    if status == "ready" and unresolved > 0:
        findings.append(
            make_finding(
                "error",
                "ready_with_open_questions",
                path,
                f"Spec status is `ready` but has {unresolved} unresolved open question(s).",
            )
        )
    if deferred > 0:
        findings.append(
            make_finding("warning", "open_questions_deferred", path, f"Spec has {deferred} deferred open question(s).")
        )
    return unresolved, deferred


def validate_impl_file(path: Path, expected_num: str, findings: list[Finding]) -> str:
    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return ""

    validate_required_metadata(path, metadata, ["type", "description", "status", "created", "updated"], findings)
    if metadata.get("type") != "plan-implementation":
        findings.append(
            make_finding("error", "invalid_type", path, "Implementation `type` must be `plan-implementation`.")
        )

    status = metadata_text(metadata, "status")
    if status is not None and status not in IMPL_STATUSES:
        findings.append(
            make_finding("error", "invalid_status", path, "Implementation status must be `active`, `completed`, or `abandoned`.")
        )

    validate_timestamp("created", metadata_text(metadata, "created"), path, findings)
    validate_timestamp("updated", metadata_text(metadata, "updated"), path, findings)
    check_sections(path, content, IMPL_SECTIONS, "Implementation", findings)
    check_prefix(path, expected_num, findings)

    _validate_task_table(path, content, status, findings)
    if status == "abandoned" and not re.search(r"abandon|dropped|reason|why", content, re.IGNORECASE):
        findings.append(make_finding("warning", "abandoned_without_reason", path, "Abandoned plan should document rationale."))
    return content


def _validate_task_table(path: Path, content: str, status: str | None, findings: list[Finding]) -> None:
    if tasks_section_has_checkboxes(content):
        findings.append(
            make_finding("error", "task_checkboxes_not_allowed", path, "`## Tasks` must use status table rows, not checkboxes.")
        )
    if TASK_STATUS_LINE not in get_section(content, "Tasks"):
        findings.append(
            make_finding("error", "missing_task_status_legend", path, "`## Tasks` must list allowed task statuses.")
        )
    for link, task_status in extract_task_table_statuses(content).items():
        if task_status not in TASK_STATUSES:
            findings.append(
                make_finding("error", "invalid_task_status", path, f"`{link}` uses invalid task status `{task_status}`.")
            )
    if status == "completed":
        if count_unchecked_boxes(content) > 0:
            findings.append(
                make_finding("error", "completed_has_unchecked", path, "Status `completed` but unchecked acceptance criteria remain.")
            )
        incomplete = [s for s in extract_task_table_statuses(content).values() if s != "completed"]
        if incomplete:
            findings.append(
                make_finding("error", "completed_has_incomplete_tasks", path, "Status `completed` but not all task rows are `completed`.")
            )


# ---------- Legacy-mode plan files ----------

LEGACY_DRAFT_SECTIONS = [
    "Goal",
    "Context / Why",
    "What We Want To Achieve (Outcomes)",
    "Summary Of Work Needed",
    "Key Principles / Constraints",
    "Open Questions",
]
LEGACY_ACTIVE_SECTIONS = ["Goal", "Context / Why", "Tasks", "Acceptance Criteria", "Notes"]


def validate_legacy_plan_file(path: Path, expected_num: str, stage: str, findings: list[Finding]) -> str:
    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return ""

    if metadata.get("type") != "plan":
        findings.append(make_finding("error", "invalid_type", path, "Legacy plan frontmatter `type` must be `plan`."))
    if not metadata.get("description"):
        findings.append(make_finding("error", "missing_frontmatter", path, "Frontmatter `description` is required."))

    # status/timestamps may live in frontmatter or body for legacy files.
    status = metadata_text(metadata, "status") or (extract_field_value(content, "Status") or "").strip("`") or None
    if stage == "draft" and status is not None and status != "draft":
        findings.append(make_finding("error", "invalid_status", path, "Legacy draft status must be `draft`."))
    if stage == "active" and status is not None and status not in IMPL_STATUSES:
        findings.append(make_finding("error", "invalid_status", path, "Legacy plan status must be `active`, `completed`, or `abandoned`."))

    validate_timestamp("created", metadata_text(metadata, "created") or extract_field_value(content, "Created"), path, findings)
    validate_timestamp("updated", metadata_text(metadata, "updated") or extract_field_value(content, "Updated"), path, findings)

    sections = LEGACY_DRAFT_SECTIONS if stage == "draft" else LEGACY_ACTIVE_SECTIONS
    check_sections(path, content, sections, "Legacy plan", findings, severity="warning")
    check_prefix(path, expected_num, findings)
    if stage == "active" and status == "completed" and count_unchecked_boxes(content) > 0:
        findings.append(
            make_finding("error", "completed_has_unchecked", path, "Status `completed` but unchecked checklist items remain.")
        )
    return content


# ---------- Task files ----------

TASK_SECTIONS = ["Required Context", "Objective", "Scope", "Steps", "Verification"]


def validate_task_file(
    path: Path, expected_num: str, findings: list[Finding], legacy: bool = False
) -> dict[str, Any] | None:
    match = TASK_FILE_RE.match(path.name)
    if not match:
        findings.append(
            make_finding("error", "invalid_task_filename", path, "Task filename must match taskNNN-NN-short-name.md.")
        )
        return None
    if match.group("num") != expected_num:
        findings.append(make_finding("error", "task_prefix_mismatch", path, "Task filename NNN must match parent plan number."))

    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return None

    if metadata.get("type") != "task":
        findings.append(make_finding("error", "invalid_type", path, "Task frontmatter `type` must be `task`."))

    if legacy:
        # Lenient old-style task: type + description + body timestamps only.
        if not metadata.get("description"):
            findings.append(make_finding("error", "missing_frontmatter", path, "Frontmatter `description` is required."))
        validate_timestamp("Created", extract_field_value(content, "Created"), path, findings)
        validate_timestamp("Updated", extract_field_value(content, "Updated"), path, findings)
        return metadata

    validate_required_metadata(path, metadata, ["type", "description", "status", "created", "updated"], findings)
    status = metadata_text(metadata, "status")
    if status is not None and status not in TASK_STATUSES:
        findings.append(
            make_finding("error", "invalid_task_status", path, f"Task status must be one of: {', '.join(sorted(TASK_STATUSES))}.")
        )
    validate_timestamp("created", metadata_text(metadata, "created"), path, findings)
    validate_timestamp("updated", metadata_text(metadata, "updated"), path, findings)
    check_sections(path, content, TASK_SECTIONS, "Task", findings)
    if not re.search(r"^#\s+Execution\s*$", content, re.MULTILINE):
        findings.append(make_finding("error", "missing_section", path, "Task missing section: `# Execution`."))
    return metadata


# ---------- Optional files ----------


def validate_prompt_file(path: Path, findings: list[Finding], check_sections_flag: bool = True) -> None:
    try:
        metadata, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return
    validate_timestamp("created", metadata_text(metadata, "created"), path, findings)
    validate_timestamp("updated", metadata_text(metadata, "updated"), path, findings)
    if not check_sections_flag:
        return
    for section in ["Original prompt", "Interpreted prompt"]:
        if not has_section(content, section):
            findings.append(
                make_finding("warning", "prompt_missing_section", path, f"Prompt should include `## {section}`.")
            )


def validate_followup_file(path: Path, findings: list[Finding]) -> None:
    try:
        _, content = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return
    if not any(has_section(content, s) for s in ["Blockers", "Important", "Minor"]):
        findings.append(
            make_finding("warning", "followup_missing_section", path, "Follow-up should group items under Blockers/Important/Minor.")
        )


def validate_review_file(path: Path, expected_num: str, findings: list[Finding]) -> None:
    try:
        metadata, _ = load_markdown(path)
    except Exception as exc:  # noqa: BLE001
        findings.append(make_finding("error", "frontmatter_parse_failed", path, f"Unable to parse: {exc}"))
        return
    review_type = metadata.get("type")
    if review_type not in REVIEW_TYPES:
        findings.append(make_finding("error", "invalid_type", path, "Review `type` must be `review` or `review-summary`."))
    if review_type == "review-summary":
        validate_required_metadata(path, metadata, ["type", "date", "plan", "sources"], findings)
    else:
        validate_required_metadata(path, metadata, ["type", "reviewer", "date", "plan", "status"], findings)
    validate_timestamp("date", metadata_text(metadata, "date"), path, findings)


# ---------- Folder driver ----------


def _gather_root_files(plan_dir: Path, findings: list[Finding]) -> dict[str, Path | None]:
    found: dict[str, Path | None] = {
        "prompt": None, "spec": None, "impl": None, "draft": None, "active": None, "followup": None,
    }
    for child in plan_dir.iterdir():
        if not (child.is_file() and child.suffix == ".md"):
            continue
        name = child.name
        if PROMPT_RE.match(name):
            found["prompt"] = child
        elif SPEC_RE.match(name):
            found["spec"] = child
        elif IMPL_RE.match(name):
            found["impl"] = child
        elif FOLLOWUP_RE.match(name):
            found["followup"] = child
        elif DRAFT_RE.match(name):
            found["draft"] = child
        elif ACTIVE_RE.match(name):
            found["active"] = child
        else:
            findings.append(make_finding("warning", "unknown_markdown_file", child, "Unrecognized markdown file in plan root."))
    return found


def validate_plan_folder(plan_dir: Path) -> dict[str, Any]:
    findings: list[Finding] = []
    files_checked = 0

    folder_match = FOLDER_RE.match(plan_dir.name)
    if not folder_match:
        findings.append(make_finding("error", "invalid_folder_name", plan_dir, "Plan folder must match `planNNN-short-description`."))
        return _result(plan_dir, findings, 0)
    expected_num = folder_match.group("num")

    files = _gather_root_files(plan_dir, findings)
    mode = "current" if (files["spec"] or files["impl"]) else "legacy" if (files["draft"] or files["active"]) else "current"

    if mode == "legacy":
        findings.append(make_finding("warning", "legacy_plan_format", plan_dir, "Plan uses legacy file names; migrate to spec/implementation."))
    else:
        for legacy_key, name in (("draft", "draft"), ("active", "implementation")):
            if files[legacy_key]:
                findings.append(
                    make_finding("warning", "deprecated_filename", files[legacy_key], f"Legacy {name} filename; rename to the new convention.")
                )

    spec_unresolved = 0
    impl_content = ""

    if files["prompt"]:
        files_checked += 1
        validate_prompt_file(files["prompt"], findings, check_sections_flag=(mode == "current"))
    elif mode == "current":
        findings.append(make_finding("warning", "missing_prompt_file", plan_dir, f"Expected `plan{expected_num}-prompt.md`."))

    if mode == "current":
        if files["spec"]:
            files_checked += 1
            result = validate_spec_file(files["spec"], expected_num, findings)
            if result is not None:
                spec_unresolved = result[0]
        else:
            findings.append(make_finding("warning", "missing_spec_file", plan_dir, f"Expected `plan{expected_num}-spec.md`."))
        if files["impl"]:
            files_checked += 1
            impl_content = validate_impl_file(files["impl"], expected_num, findings)
        else:
            findings.append(make_finding("warning", "missing_implementation_file", plan_dir, f"Expected `plan{expected_num}-implementation.md`."))
        if files["impl"] and spec_unresolved > 0:
            findings.append(
                make_finding(
                    "error",
                    "open_questions_block_implementation",
                    files["spec"] if files["spec"] else plan_dir,
                    f"Implementation exists but spec has {spec_unresolved} unresolved open question(s).",
                )
            )
    else:
        if files["draft"]:
            files_checked += 1
            validate_legacy_plan_file(files["draft"], expected_num, "draft", findings)
        if files["active"]:
            files_checked += 1
            impl_content = validate_legacy_plan_file(files["active"], expected_num, "active", findings)

    if files["followup"]:
        files_checked += 1
        validate_followup_file(files["followup"], findings)

    # Tasks
    tasks_dir = plan_dir / "tasks"
    task_metadata: dict[str, dict[str, Any]] = {}
    task_files: list[Path] = []
    if tasks_dir.is_dir():
        task_files = sorted(p for p in tasks_dir.iterdir() if p.is_file() and p.suffix == ".md")
        for task_file in task_files:
            files_checked += 1
            metadata = validate_task_file(task_file, expected_num, findings, legacy=(mode == "legacy"))
            if metadata is not None:
                task_metadata[f"tasks/{task_file.name}"] = metadata

    if task_files and impl_content and not contains_task_file_reference(impl_content):
        findings.append(
            make_finding("warning", "task_files_not_referenced", plan_dir, "Task files exist but the plan does not reference them.")
        )

    # Cross-check table status vs task frontmatter (current mode only)
    if mode == "current" and impl_content:
        for link, table_status in extract_task_table_statuses(impl_content).items():
            metadata = task_metadata.get(link)
            if metadata is None:
                findings.append(make_finding("error", "missing_task_file", files["impl"] or plan_dir, f"Plan references missing task file `{link}`."))
                continue
            file_status = metadata_text(metadata, "status")
            if file_status != table_status:
                findings.append(
                    make_finding(
                        "error",
                        "task_status_mismatch",
                        files["impl"] or plan_dir,
                        f"`{link}` table status `{table_status}` does not match task frontmatter `{file_status}`.",
                    )
                )

    # Reviews
    review_dir = plan_dir / "review"
    if review_dir.is_dir():
        for review_file in sorted(p for p in review_dir.iterdir() if p.is_file() and p.suffix == ".md"):
            files_checked += 1
            validate_review_file(review_file, expected_num, findings)

    return _result(plan_dir, findings, files_checked)


def _result(plan_dir: Path, findings: list[Finding], files_checked: int) -> dict[str, Any]:
    errors = [f for f in findings if f.severity == "error"]
    warnings = [f for f in findings if f.severity == "warning"]
    return {
        "target": str(plan_dir),
        "summary": {"errors": len(errors), "warnings": len(warnings), "files_checked": files_checked},
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
