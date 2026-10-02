---
type: task
description: "Task 064-01 — map audio/* input content to Interactions audio parts; reject unmappable content"
status: not-started
created: 2026-10-02T20:20:00+02:00
updated: 2026-10-02T20:20:00+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards` (executor role)

Read: `plan064-spec.md` §0, §3, §7 Q1. Code: `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
(`MapInput` filter ~L377, `AddAssistantInputSteps` ~L407, `MapTurnContent` ~L440-520,
`MapFunctionResultContent` ~L660, `MapImageContent`/`MapDocumentContent` ~L682-708).
Docs: https://ai.google.dev/gemini-api/docs/audio and https://ai.google.dev/gemini-api/docs/interactions.md.txt.

## Objective

User-message audio reaches the API as `{type:"audio", data|uri, mime_type}`, and content the
adapter cannot map raises an error instead of disappearing.

## Scope

Included:
- `MapAudioContent(DataContent)` / `MapAudioContent(UriContent)` and the `HasTopLevelMediaType("audio")` cases in `MapTurnContent`.
- Let `"audio"` through the user-input filter.
- Media type normalisation only if the docs require it (e.g. `audio/mpeg` vs `audio/mp3`). Record the decision in the Executor Notes.
- `NotSupportedException` (message names the media type) for `DataContent`/`UriContent` that is neither image, PDF nor audio, in user turns.
  Decide and document what happens for assistant-turn and function-result content. Do not break existing image/PDF behaviour or tool round-trips.

Excluded: Files API, `HostedFileContent`, `transcription_config`, audio output.

## Steps

1. Add the audio mapping and filter change.
2. Replace the silent drop with `NotSupportedException` for unmappable media in user turns.
3. Unit tests (product behaviour / external protocol assumption, per the parent testing guidelines):
   one parameterized test that audio `DataContent` and `UriContent` serialize to the documented
   part shape, and one test that an unsupported media type (e.g. `video/mp4`) throws.
   Put them in the existing content-mapping test file. Do not add plan-named tests.

## Verification

- `dotnet build` exits 0 with no new warnings.
- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` exits 0.
- `rg -n 'HasTopLevelMediaType\("audio"\)' src` shows the new cases.
- Manual: the serialized request JSON from the audio test contains `"type":"audio"` with `data`+`mime_type` (inline) or `uri`+`mime_type` (URI), never both.

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
