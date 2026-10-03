---
created: 2026-10-03T15:55:00+02:00
updated: 2026-10-03T15:55:00+02:00
---
# Plan 065 Prompt

## Original prompt

(From a bughunt review of CatHerder (parent repo), issue 4: "The Gemini call-ID→name map may
duplicate what the adapter already derives". After investigation in the parent repo, the map
turned out to be required, but unbounded.)

Look at 4 - Investigate and explain details to me here.
run that test
Write this as plan in submodule

(Suggestions accepted in the same conversation:)
- Keep the map (a live API experiment showed Gemini rejects function_result without "name",
  despite the reference marking "name" optional).
- Prune it: only remember calls Gemini returns in a response; drop an entry only after a
  request carrying its result succeeded.
- Document why the map exists (the reference is wrong in practice).
- Optionally keep the experiment as a gated live test.

## Interpreted prompt

Investigate and improve the call-ID-to-name map in `GeminiInteractionsChatClient`
(`catherder.agents.ai.gemini.function_names_by_call_id`, stored in the AgentSession state bag
and persisted through the parent repo's `continuation.json`).

Background: the map lets the adapter fill in the required `name` of a `function_result` when
the request does not contain the call it answers (Gemini keeps the history server-side, so a
tool-loop follow-up carries only results). A live experiment against the real API proved the
name is required: a follow-up with results that have no `name` is rejected with HTTP 400
"function_response.name: Name cannot be empty", also when streaming, although the published
reference lists `name` as optional.

The map is required but unbounded: it records every call the adapter has ever seen, including
all calls of carried history from other providers (291 entries, 15.7 KB, in one parent-repo
session), and nothing is ever removed.

Plan the pruning of the map so it stays small, without losing the ability to answer
follow-ups, retries after a failed request, and sessions restored after a restart.
