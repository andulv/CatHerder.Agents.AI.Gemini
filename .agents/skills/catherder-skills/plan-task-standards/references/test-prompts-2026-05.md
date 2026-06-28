---
type: reference
description: "Trigger and non-trigger prompt examples for plan-task-standards skill"
snapshot_date: 2026-06-26
sources:
  - SKILL.md
---
# Test Prompts (2026-05)

Use these prompts to sanity-check skill routing behavior.

## Should Trigger

- "Create a new `plan014` folder with prompt and spec files."
- "Turn this spec into an implementation plan with tasks."
- "Validate this `.catherder/plans/plan009-...` folder and report format issues."
- "Fix timestamp format and status values in my plan/task files."
- "Do task files under `tasks/` follow CatHerder naming conventions?"
- "Resolve the open questions and mark the spec ready for implementation."
- "Add a review file under `review/` for this plan's spec."
- "Execute task 080-03 and record the result."
- "Review task 080-03 and mark it completed if it passes."
- "Start a post-completion follow-up file for plan 080."

## Should Not Trigger

- "Write a C# unit test for pricing calculator rounding."
- "Refactor this Python parser into smaller functions."
- "Set up a VS Code extension scaffold."
- "Review API docs for Anthropic models."
- "Explain how dependency injection works in this codebase."

## Borderline (May Co-Trigger)

- "Create a skill and include instructions on plan formatting."
- "Review `.catherder/plans` quality across plans and skills."
