---
type: plan-spec
description: "Plan 065 - keep the call-ID-to-name map small: record only calls that still need a result, prune after the result left successfully, and document the live-API requirement of function_result.name"
status: ready
created: 2026-10-03T15:55:00+02:00
updated: 2026-10-03T15:55:00+02:00
---
# Plan 065 Spec — Function-Name Map Pruning

## 0. Required Context

- `plan-task-standards`; this submodule's `.agents/` instructions.
- [`plan065-prompt.md`](plan065-prompt.md)
- [`data/function-name-map-investigation.md`](data/function-name-map-investigation.md) — mechanism, live-API experiment (400 without `name`), growth numbers
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
  (`FunctionNamesByCallIdStateKey`, `RememberFunctionName(s)`, `ResolveFunctionName`,
  `TryResolveFunctionNameFromSession/Transcript`)
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/GeminiInteractionsChatClientTests.cs`
  (`Throws_WhenFunctionResultNameCannotBeResolved`, `DoesNotPersist_FunctionNameAcrossClientInstances`)
- `tests/CatHerder.Agents.AI.Gemini.IntegrationTests/` (gated live tests, user secrets
  `GOOGLE_API_KEY`, `GEMINI_INTERACTIONS_MODEL`)
- Parent context (read-only): CatHerder stores the state bag in `continuation.json`; parent
  issue 4 and plan222 (tool calls are answered at turn end).

## 1. Goal

The call-ID-to-name map lets the adapter build valid `function_result` content when the
request does not contain the call (Gemini holds the history server-side). The map stays
required, but it must stay **small**: it holds only calls whose result has not left
successfully yet, and it documents why it exists so it is not removed based on the
(published as optional, in practice required) `name` field.

## 2. Context / Why

Today every call the adapter ever sees is recorded — calls sent in requests (all carried
history, from any provider) and calls answered long ago. Nothing is pruned. One
parent-repo session reached 291 entries (15.7 KB) that are rewritten into
`continuation.json` after every turn. A live experiment (see data file) proved the name
is required: results without `name` are rejected with HTTP 400, so the map cannot be
dropped — but almost all of its content is dead weight.

## 3. What We Want To Achieve (Outcomes)

1. **Record only what is needed.** A call is added to the map only when its result may
   have to be sent in a later request without the call present:
   - `function_call` steps returned by Gemini in a response or stream (the tool loop
     answers them in the next service call);
   - calls sent in a request are **not** recorded; their results are in the same request
     and resolve through the transcript lookup.
2. **Prune only after success.** A call's entry is removed when a request containing its
   `function_result` completed with a success status from the API. A failed request keeps
   the entries, so a retry can rebuild the same request. Streaming counts as success when
   the stream was established (first event received) — the results are then on their way.
3. **Bounded size.** After any completed request, the map holds at most the calls whose
   results have not been sent successfully (in practice: the open calls of the current
   tool loop, plus calls answered by results that have not left yet).
4. **Documented.** The map's declaration and `ResolveFunctionName` carry a comment stating
   that `function_result.name` is required in practice although the reference marks it
   optional, with the observed 400 text, and why pruning happens only after success.
5. **Gated live test.** The integration test project gets a test proving the external
   assumption: a server-side tool loop whose follow-up omits `name` is rejected with 400
   (skipped without `GOOGLE_API_KEY`/`GEMINI_INTERACTIONS_MODEL`, like the other live
   tests). It is the reason the map exists.
6. **Behavior preserved.** Tool loops, retry-after-failure and restored sessions
   (`continuation.json` state bag) keep working; existing unit tests pass, extended with
   pruning cases.

## 4. Key Principles / Constraints

- No protocol changes: the wire content is unchanged; only the session state shrinks.
- The map stays a session-statebag mechanism (per run, persisted by the host). No static
  or instance-level caches (see plan049 and
  `DoesNotPersist_FunctionNameAcrossClientInstances`).
- Reading and writing the map stays internal to the adapter.
- Failure paths must not lose needed entries: an HTTP error, a malformed response or a
  cancelled stream leaves the map as it was for that request.
- Follow the testing guidelines: the pruning rules are unit-tested against the fake HTTP
  handler; the API requirement is covered by the gated live test only.

## 5. Out of Scope

- Parent-repo changes (`continuation.json` format, plan222's turn closure). Old, larger
  maps in existing `continuation.json` files keep working; entries that are no longer
  needed disappear on their own as new successes prune them.
- The transcript and `AdditionalProperties` lookups (they stay, in the same order).
- Sending `name: ""` or omitting `name` on the wire.
- Other providers.

## 6. Implementation Notes

Direction only.

- The map write sites: `RememberFunctionName` at the response/stream call sites stays;
  the call site in request content mapping (`:482`) is removed.
- Pruning: after a successful `GetResponseAsync`/established stream, remove the call ids
  whose `FunctionResultContent` were part of that request's input. Note the input may
  contain several results in one tool-role message; collect ids before mapping content.
- The unit tests need a scenario where the same client instance answers a call, then
  sends the result (map entry used, then removed), and one where the request fails
  (entry kept). Mirror the existing `RecordingHandler` style.
- Consider whether `RememberFunctionNames(update.Contents)` during streaming can see a
  call twice (call step and delta) — a `HashSet`-style dedupe is fine either way; the map
  write is idempotent today.

## 7. Open Questions

1. (resolved: yes, add it) Include the gated live test proving `function_result.name` is required?
2. (resolved: after an established stream) Does a streaming follow-up count as success for pruning?
