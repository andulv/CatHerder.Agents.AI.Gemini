---
type: plan-spec
description: "Plan 064 - Map inline audio input (DataContent/UriContent audio/*) to Interactions audio parts"
status: ready
created: 2026-10-02T20:05:00+02:00
updated: 2026-10-02T20:05:00+02:00
---
# Plan 064 Spec — Audio Input Mapping

## 0. Required Context

- `plan-task-standards`; this submodule's `.agents/` instructions.
- Parent effort: catherder-dev plan220 (chat audio attachments, OneDev CHD-51). The
  parent repo records the cross-project decision; this plan covers the submodule change.
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`:
  `MapTurnContent` (L465-479: only `image/*` and `application/pdf` are mapped; other
  `DataContent`/`UriContent` is silently dropped), user-input filter L377 and
  model-output filter L407 (`text|image|document`), `MapFunctionResultContent`
  L669-676, `MapImageContent`/`MapDocumentContent` L682-708.
- `Internal/GeminiInteractionsRequestModels.cs` (`GeminiInteractionContent` already
  has `Type`, `MimeType`, `Data`, `Uri`).
- Interactions API audio part (ai.google.dev/gemini-api/docs/audio, checked 2026-10-02):
  `{"type":"audio","data":"<base64>","mime_type":"audio/mp3"}` or
  `{"type":"audio","uri":"...","mime_type":...}` (mutually exclusive). Supported:
  wav, mp3, aiff, aac, ogg, flac, webm. m4a not listed. Inline request limit 20 MB total.

## 1. Goal

`DataContent` and `UriContent` with an `audio/*` media type in a user message
reach the Interactions API as `type: "audio"` input parts.

## 2. Context / Why

CatHerder (the main consumer) adds audio attachments (plan220). Today the adapter
drops audio silently, so the model never sees it and nothing reports an error.
Silently dropping content also violates the principle of least surprise for any
MEAI consumer.

## 3. What We Want To Achieve (Outcomes)

- `audio/*` `DataContent` maps to `{type:"audio", data, mime_type}`; `audio/*`
  `UriContent` maps to `{type:"audio", uri, mime_type}`. Both are kept by the
  user-input filter.
- Media type is normalised where Gemini expects a specific value (e.g. `audio/mpeg` →
  as documented; verify against the docs/live call and record the result in `data/`).
- Content the adapter cannot map (e.g. `video/*`, other application types) is no
  longer silently dropped: it gets a clear, documented behaviour (see question 1).
- A pre-flight check rejects requests whose serialized inline payload exceeds the
  documented 20 MB limit with a clear `GeminiApiException`-style error naming the
  limit, instead of an opaque HTTP failure (see question 2).
- Unit tests pin the audio mapping (inline + URI) and the unsupported-content
  behaviour. One live integration test sends a short audio clip and asserts a
  sensible response (opt-in like the existing live tests).
- The live check records whether m4a (`audio/mp4` / `audio/aac` / `audio/x-m4a`) is accepted.
- README documents multimodal input support (image, PDF, audio).

## 4. Key Principles / Constraints

- Follow MEAI adapter conventions and the existing image/document mapping style.
- No breaking change for image/PDF mapping.
- Wire field names and media types verified against current Interactions docs or a
  live call before use.

## 5. Out of Scope

- Files API (upload/resumable/get/delete) and `HostedFileContent`.
- `transcription_config` / `gemini-3.5-transcribe`.
- Audio output.
- Audio inside function results (`MapFunctionResultContent`) unless trivial. Decide in implementation.

## 6. Implementation Notes

- Add `case DataContent/UriContent when HasTopLevelMediaType("audio")` next to the
  image cases, with a `MapAudioContent` pair, and add `"audio"` to the L377 filter.
- Pre-flight size check: the serialized request length is known when the request
  message is created (`CreateInteractionRequestMessage`).

## 7. Open Questions

1. Behaviour for unmappable content? (resolved: throw `NotSupportedException` naming
   the media type, consistent with how other MEAI adapters reject unsupported content.
   If a consumer depends on silent dropping, note it in the changelog.)
2. Pre-flight size limit: hard-coded or configurable? (resolved: an option on
   `GeminiInteractionsChatClientOptions` with default 20 MB (decimal, per docs),
   `null` disables the check.)
