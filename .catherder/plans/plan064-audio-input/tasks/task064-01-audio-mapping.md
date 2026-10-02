---
type: task
description: "Task 064-01 — map audio/* input content to Interactions audio parts; reject unmappable content"
status: implemented
created: 2026-10-02T20:20:00+02:00
updated: 2026-10-02T20:41:33+02:00
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
By: pi (gpt-5.x) @ 2026-10-02T20:41:33+02:00

Work done:

- `MapTurnContent` gained a `rejectUnsupportedContent` parameter. User turns (single-turn
  `MapInput` path and multi-turn `MapInputSteps` path) call it with `true`;
  assistant-turn and function-result paths call it with `false`.
- Added `case DataContent/UriContent when HasTopLevelMediaType("audio")` cases with a
  `MapAudioContent` pair (inline → `data`, URI → `uri`), mirroring the image/document mappers.
- Added `"audio"` to the user-input filter in `MapInputSteps`. The assistant filter
  (`text|image|document`) and `MapFunctionResultContent` (text + image) are unchanged.
- New `UnsupportedMediaType` helper throws `NotSupportedException` naming the media type
  for `DataContent`/`UriContent` that is neither `image/*`, `audio/*`, nor
  `application/pdf`, but only when `rejectUnsupportedContent` is set.
- Tests (in `GeminiInteractionsChatClientTests.cs`): one `[Theory]` covering inline + URI
  audio in a multi-turn user turn asserting the documented part shape (exactly one of
  `data`/`uri`, plus `mime_type`); one test that a `video/mp4` `DataContent` in a user
  turn throws `NotSupportedException` naming the media type with no HTTP request sent;
  one test pinning the documented assistant-turn drop behaviour.

Decisions recorded:

- **Media-type normalisation:** none. Docs checked 2026-10-02 (see
  `../data/docs-check-2026-10-02.md`): the Interactions API expects the actual MIME type;
  `audio/mpeg` is listed as-is (no documented mapping to `audio/mp3`). Pass-through is
  consistent with image/PDF handling.
- **Assistant turns:** unmappable media is dropped (not thrown). Assistant turns are
  replayed history; this package maps no audio/video parts into `model_output` steps
  (audio output is out of scope per spec §5). Throwing there would break history replay.
- **Function results:** `MapFunctionResultContent` continues to accept text + image only.
  Audio inside tool results is dropped (spec §5: excluded unless trivial; the API
  acceptance of audio parts in `function_result` results is not verified, so no change).
- Changelog note for consumers: user-turn `DataContent`/`UriContent` with a media type
  that is not `image/*`, `audio/*`, or `application/pdf` previously fell through silently;
  it now throws `NotSupportedException`.

## Executor Verification
By: pi (gpt-5.x) @ 2026-10-02T20:41:33+02:00

- `dotnet build` → exit 0, "Build succeeded. 0 Warning(s) 0 Error(s)".
- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` → exit 0, "Passed! - Failed: 0,
  Passed: 84, Skipped: 0, Total: 84" (4 new test cases).
- `rg -n 'HasTopLevelMediaType\("audio"\)' src` → the two new cases at
  `GeminiInteractionsChatClient.cs` L481/L485.
- Manual (scratch console app referencing the package, not committed): serialized request
  for a single user turn with inline audio contains
  `{"type":"audio","mime_type":"audio/wav","data":"AQID"}` (data + mime_type, no uri),
  and for URI audio contains
  `{"type":"audio","mime_type":"audio/wav","uri":"https://storage.example.com/clips/demo.wav"}`
  (uri + mime_type, no data). Neither part contains both fields.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
