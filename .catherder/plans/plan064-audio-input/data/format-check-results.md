# Format check — inline audio (plan064 T03)

Model: `gemini-3.1-flash-lite-preview` · Endpoint: `https://generativelanguage.googleapis.com/v1beta/` · Api-Revision: 2026-05-20

Clip: 3 s 440 Hz sine tone, 16 kHz mono (checked-in `audio/clip.wav`, converted per format).
Question sent with every request: "Is this audio clip silent or does it contain sound? Answer with exactly one word: silent or sound."

| Media type sent | File | Bytes | HTTP | Accepted | Response / error |
|---|---|---|---|---|---|
| `audio/wav` | .wav | 96078 | 200 | yes | sound |
| `audio/mp3` | .mp3 | 9513 | 200 | yes | sound |
| `audio/ogg` | .ogg | 6086 | 200 | yes | sound |
| `audio/flac` | .flac | 26773 | 200 | yes | sound |
| `audio/webm` | .webm | 27848 | 200 | yes | sound |
| `audio/mp4` | .m4a | 19993 | 200 | yes | sound |
| `audio/aac` | .m4a | 19993 | 200 | yes | sound |
| `audio/x-m4a` | .m4a | 19993 | 200 | yes | sound |
| `audio/m4a` | .m4a | 19993 | 200 | yes | sound |
