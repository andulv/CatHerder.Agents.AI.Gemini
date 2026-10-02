#!/usr/bin/env python3
"""Plan 064 T03 — scratch format check (not a project test).

Sends the checked-in test clip inline as an `audio` part in a Gemini Interactions
request, once per format/media type pair, and records accepted/rejected per pair.

Requires: GOOGLE_API_KEY and GEMINI_INTERACTIONS_MODEL environment variables.
Usage: python3 format-check.py [output.md]
Writes results to the given markdown file (default: format-check-results.md next
to this script) and prints a summary table to stdout.
"""

import base64
import json
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import urllib.error

ENDPOINT = os.environ.get(
    "GEMINI_INTERACTIONS_BASE_URL", "https://generativelanguage.googleapis.com/v1beta/"
)
API_KEY = os.environ["GOOGLE_API_KEY"]
MODEL = os.environ["GEMINI_INTERACTIONS_MODEL"]

# (ffmpeg codec, file ext, media type to send)
FORMATS = [
    ("pcm_s16le", "wav", "audio/wav"),
    ("libmp3lame", "mp3", "audio/mp3"),
    ("libvorbis", "ogg", "audio/ogg"),
    ("flac", "flac", "audio/flac"),
    ("libopus", "webm", "audio/webm"),
    # m4a candidates: the task asks for audio/mp4, audio/aac, audio/x-m4a;
    # audio/m4a is listed on the docs' "Audio understanding" page, so include it.
    ("aac", "m4a", "audio/mp4"),
    ("aac", "m4a", "audio/aac"),
    ("aac", "m4a", "audio/x-m4a"),
    ("aac", "m4a", "audio/m4a"),
]

QUESTION = "Is this audio clip silent or does it contain sound? Answer with exactly one word: silent or sound."


def convert(clip_wav: str, codec: str, ext: str, out_dir: str) -> str:
    out_path = os.path.join(out_dir, f"clip.{ext}")
    subprocess.run(
        ["ffmpeg", "-y", "-v", "quiet", "-i", clip_wav, "-c:a", codec, out_path],
        check=True,
    )
    return out_path


def send_inline(path: str, media_type: str) -> dict:
    with open(path, "rb") as f:
        data = base64.b64encode(f.read()).decode()

    payload = json.dumps({
        "model": MODEL,
        "input": [
            {"type": "text", "text": QUESTION},
            {"type": "audio", "data": data, "mime_type": media_type},
        ],
    }).encode()

    request = urllib.request.Request(
        ENDPOINT + "interactions",
        data=payload,
        headers={
            "Content-Type": "application/json",
            "x-goog-api-key": API_KEY,
            "Api-Revision": "2026-05-20",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            body = response.read().decode()
            root = json.loads(body)
            return {"status": response.status, "body": root}
    except urllib.error.HTTPError as ex:
        return {"status": ex.code, "body": json.loads(ex.read().decode() or "{}")}


def extract_text(root: dict) -> str:
    parts = []
    for step in root.get("steps", []):
        if step.get("type") == "model_output":
            for block in step.get("content", []):
                if block.get("type") == "text":
                    parts.append(block.get("text", ""))
    return " ".join(parts).strip()


def main() -> int:
    clip_wav = os.path.join(
        os.path.dirname(__file__),
        "..", "..", "..", "..",
        "tests", "CatHerder.Agents.AI.Gemini.IntegrationTests", "audio", "clip.wav",
    )
    clip_wav = os.path.abspath(clip_wav)
    if not os.path.exists(clip_wav):
        print(f"clip not found: {clip_wav}", file=sys.stderr)
        return 2

    out_file = sys.argv[1] if len(sys.argv) > 1 else "format-check-results.md"
    results = []

    with tempfile.TemporaryDirectory() as tmp:
        for codec, ext, media_type in FORMATS:
            path = convert(clip_wav, codec, ext, tmp)
            size = os.path.getsize(path)
            print(f"checking {media_type} ({ext}, {size} bytes)...", file=sys.stderr)
            outcome = send_inline(path, media_type)
            status = outcome["status"]
            body = outcome["body"]
            text = extract_text(body) if status == 200 else ""
            error = ""
            if status != 200:
                err = body.get("error", {})
                error = f"{err.get('status', '')} / {err.get('code', '')}: {err.get('message', '')}"
            results.append((media_type, ext, size, status, text, error))

    lines = [
        "# Format check — inline audio (plan064 T03)",
        "",
        f"Model: `{MODEL}` · Endpoint: `{ENDPOINT}` · Api-Revision: 2026-05-20",
        "",
        "Clip: 3 s 440 Hz sine tone, 16 kHz mono (checked-in `audio/clip.wav`, converted per format).",
        "Question sent with every request: " + json.dumps(QUESTION),
        "",
        "| Media type sent | File | Bytes | HTTP | Accepted | Response / error |",
        "|---|---|---|---|---|---|",
    ]
    for media_type, ext, size, status, text, error in results:
        accepted = "yes" if status == 200 else "no"
        detail = text if status == 200 else error
        detail = detail.replace("|", "\\|").replace("\n", " ")
        lines.append(f"| `{media_type}` | .{ext} | {size} | {status} | {accepted} | {detail} |")

    with open(out_file, "w") as f:
        f.write("\n".join(lines) + "\n")

    print(f"\nwrote {out_file}")
    for media_type, _ext, _size, status, text, error in results:
        print(f"  {media_type:15} HTTP {status}  {'accepted' if status == 200 else 'rejected: ' + error[:80]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
