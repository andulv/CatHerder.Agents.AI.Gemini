---
type: plan-implementation
description: "Plan 064 - Map inline audio input to Interactions audio parts, reject unmappable content, pre-flight size check"
status: active
created: 2026-10-02T20:20:00+02:00
updated: 2026-10-02T20:46:53+02:00
---
# Plan 064 Implementation — Audio Input Mapping

## 0. Required Context

- Spec: `plan064-spec.md` (outcomes, resolved questions 1-2).
- `plan-task-standards` (executor role); `.agents/project.instructions.md` (build/test commands).
- Parent: catherder-dev plan220 consumes this. Work happens on submodule branch
  `plan064-audio-input` inside the parent worktree
  `/home/anders/source/agent/.worktrees/plan220-chat-audio-attachments/submodules/CatHerder.Agents.AI.Gemini`.
- Build: `dotnet build`. Unit: `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests`.
  Live: `GOOGLE_API_KEY=... GEMINI_INTERACTIONS_MODEL=... dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests`.
- One commit per task, subject prefixed `task064-NN:`.

## 1. Tasks

Allowed task statuses: not-started, in-progress, blocked, implemented, reviewed, completed.

| Status | Task |
|---|---|
| `implemented` | [Task P064-T01: map audio input, reject unmappable content](tasks/task064-01-audio-mapping.md) |
| `implemented` | [Task P064-T02: pre-flight inline request size check](tasks/task064-02-request-size-preflight.md) |
| `not-started` | [Task P064-T03: live audio verification, m4a check, README](tasks/task064-03-live-verification-readme.md) |

## 2. Task Parallelism

T01 → T02 → T03, in sequence (all touch `GeminiInteractionsChatClient.cs`; T03 verifies both).

## 3. Acceptance Criteria

- [ ] `audio/*` `DataContent`/`UriContent` in user messages is sent as `type:"audio"` parts.
- [ ] Unmappable content throws `NotSupportedException` naming the media type (no silent drop).
- [ ] Requests over the configured inline limit (default 20 MB) fail before sending, with a clear error; `null` disables the check.
- [ ] Unit tests pass; one opt-in live test sends audio and gets a sensible answer.
- [ ] m4a acceptance is recorded in `data/`; README lists multimodal input support and the size option.
