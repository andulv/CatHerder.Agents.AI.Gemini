---
type: reference
description: "Trigger and non-trigger prompt examples for plan-task-standards skill"
snapshot_date: 2026-03-04
sources:
  - repo/skills/plan-task-standards/SKILL.md
---
# Test Prompts (2026-03)

Use these prompts to sanity-check skill routing behavior.

## Should Trigger

- "Create a new `plan014` folder with prompt, draft, and active plan files."
- "Validate this `.instructions/plans/plan009-...` folder and report format issues."
- "Fix timestamp format and status values in my plan/task files."
- "Do task files under `tasks/` follow CatHerder naming conventions?"
- "Prepare this draft plan for execution and create proper task docs."

## Should Not Trigger

- "Write a C# unit test for pricing calculator rounding."
- "Refactor this Python parser into smaller functions."
- "Set up a VS Code extension scaffold."
- "Review API docs for Anthropic models."
- "Summarize git workflow for release branches."

## Borderline (May Co-Trigger)

- "Create a skill and include instructions on plan formatting."
- "Review `.instructions` quality across plans and skills."
