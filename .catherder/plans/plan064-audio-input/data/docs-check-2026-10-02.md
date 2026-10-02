# Docs check — audio input (2026-10-02)

Sources checked:

- https://ai.google.dev/gemini-api/docs/audio (Audio understanding)
- https://ai.google.dev/gemini-api/docs/interactions/audio.md.txt (Interactions API audio page)

## Findings

1. **Audio input part shape** (Interactions API, `Api-Revision: 2026-05-20`, REST):
   - Inline: `{"type":"audio","data":"<base64>","mime_type":"audio/mp3"}`
   - URI: `{"type":"audio","uri":"...","mime_type":"audio/mp3"}`
   - `data` and `uri` are mutually exclusive (docs show exactly one of the two per part).
2. **Inline request size**: "Maximum request size is 20 MB total (including prompts and all
   files)". Basis for the default `MaxInlineRequestBytes = 20_000_000` (decimal MB).
3. **Supported formats**:
   - Interactions audio page: WAV `audio/wav`, MP3 `audio/mp3`, AIFF `audio/aiff`,
     AAC `audio/aac`, OGG Vorbis `audio/ogg`, FLAC `audio/flac`.
   - Audio understanding page (wider list): additionally `audio/mpeg`, `audio/m4a`,
     `audio/l16`, `audio/opus`, `audio/alaw`, `audio/mulaw`, `audio/webm`.
4. **Media-type normalisation**: not required by the docs. The API expects the actual
   MIME type — `audio/mpeg` is listed as-is (no documented mapping to `audio/mp3`).
   Decision: pass the media type through unchanged, consistent with image/PDF handling.
5. **m4a**: not listed on the Interactions audio page; `audio/m4a` is listed on the
   Audio understanding page. Live acceptance is verified in T03 (`data/format-check.md`).
