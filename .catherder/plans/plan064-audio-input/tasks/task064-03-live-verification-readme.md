---
type: task
description: "Task 064-03 — live audio round-trip, m4a acceptance check, README multimodal docs"
status: not-started
created: 2026-10-02T20:20:00+02:00
updated: 2026-10-02T20:20:00+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards` (executor role)

Read: `plan064-spec.md` §3; `tests/CatHerder.Agents.AI.Gemini.IntegrationTests/README.md` and `GeminiInteractionsLiveTests.cs` (gating pattern); `README.md`.

## Objective

Prove against the real API that audio input works. Record which container formats are accepted.
Document multimodal input.

## Scope

Included:
- One opt-in live test (same credential gating as the existing ones): send a short generated
  audio clip (e.g. a few seconds of speech, or a sine tone with a question like "is this
  audio silent or does it contain sound?"). Keep any fixture tiny, under 100 KB, checked in under the
  integration test project. Assert a non-empty, on-topic answer.
- Scratch check (not committed as a test) under `.catherder/plans/plan064-audio-input/data/`:
  send the same clip as wav, mp3, ogg, flac, webm, and m4a (try `audio/mp4`, `audio/aac`,
  `audio/x-m4a`). Record accepted/rejected per format and media type in `data/format-check.md`.
  The parent plan220 uses this to decide m4a (spec Q3).
- README: supported input content (image, PDF, audio; inline vs URI), `NotSupportedException`
  for other media, the `MaxInlineRequestBytes` option.

Excluded: committing large audio files; Files API.

## Steps

1. Create the fixture clip (e.g. with `ffmpeg` or `espeak-ng` if available) and the live test.
2. Run the format scratch check with a real key and write `data/format-check.md`.
3. Update the README.

## Verification

- `GOOGLE_API_KEY=... GEMINI_INTERACTIONS_MODEL=gemini-3-flash-preview dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests --filter <audio test>` passes.
- Without credentials, the live test is skipped, not failed.
- `data/format-check.md` exists with one row per format and media type tried.
- README mentions audio input and the size option.

---

Everything above this line is the task specification. Everything below is the
execution record. These sections repeat per review round (e.g. `Executor Notes
(post-review)`, `Reviewer Verification (second pass)`).

# Execution

## Executor Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Executor Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
