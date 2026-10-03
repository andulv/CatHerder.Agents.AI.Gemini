---
type: task
description: "Task 065-03 — unit tests: request-carried calls not recorded; success prunes; failure keeps and retry succeeds; stream completion prunes; stream failure keeps"
status: implemented
created: 2026-10-03T18:50:52+02:00
updated: 2026-10-03T19:12:30+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards`

Resources:
- `plan065-spec.md` (outcomes 1, 2, 6)
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/GeminiInteractionsChatClientTests.cs`
  (`RecordingHandler` with `responses:` queue, `CreateHttpClient`,
  `Throws_WhenFunctionResultNameCannotBeResolved`)
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/GeminiInteractionsChatClientPhase2To4Tests.cs`
  (`CreateSseResponse`, `BuildEvent` — SSE test payloads)
- MAF abstractions: `AIAgent.CurrentRunContext` is publicly settable and flows across
  async calls; `AgentRunContext.Session.StateBag` is the map home. If constructing
  `AgentSession`/`AgentRunContext` directly is awkward (abstract base), use a minimal
  test subclass — discover exact constructor shapes at compile time.

## Objective

Pin the recording and pruning rules with offline unit tests.

## Scope

Included (in `GeminiInteractionsChatClientTests.cs`, mirroring existing style):
- **Carried calls are not recorded:** one request containing a `FunctionCallContent` and
  its `FunctionResultContent` (same call id); after a successful `GetResponseAsync` the
  state bag does not contain the call id (transcript lookup resolved it; map stayed clean).
- **Non-stream success prunes:** first response returns a `function_call` step (map gains
  the entry — assert present via the session state bag); follow-up request carries the
  result and succeeds; map no longer contains the id.
- **Non-stream failure keeps:** same setup but the follow-up returns an error status;
  map still contains the id; a retry (success response) then succeeds and prunes.
- **Stream completion prunes:** SSE response with a `function_call` event (entry
  recorded), follow-up streaming request with the result over a successful SSE response;
  after the updates are consumed, the map no longer contains the id.
- **Stream failure keeps:** follow-up streaming request that fails negotiation
  (non-2xx status); map still contains the id afterwards.
Excluded:
- Gated live test (T04); production code changes (only test code here; if a test
  reveals a T01/T02 bug, stop and report — do not silently fix outside scope).

## Steps

1. Add a small test helper to install/inspect a run context with a session state bag
   (set `AIAgent.CurrentRunContext`, reset in `finally` to avoid cross-test leakage —
   xUnit runs tests in a class sequentially, but reset anyway).
2. Write the five tests above, named after the behavior (e.g.
   `GetResponseAsync_DoesNotRecord_CarriedRequestCalls`,
   `GetResponseAsync_Prunes_AfterSuccessfulResultRequest`,
   `GetResponseAsync_KeepsEntries_WhenRequestFails_RetryThenPrunes`,
   `GetStreamingResponseAsync_Prunes_AfterStreamCompletes`,
   `GetStreamingResponseAsync_KeepsEntries_WhenNegotiationFails`).
3. Run the unit test project.

## Verification

- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` green, including the five new
  tests and the existing `Throws_WhenFunctionResultNameCannotBeResolved` /
  `DoesNotPersist_FunctionNameAcrossClientInstances`.
- Manual check: the "not recorded" test fails if the `:509` write site were restored
  (falsifiable).

---

Everything above this line is the task specification. Everything below is the
execution record.

# Execution

## Executor Notes
By: pi coding agent (GLM) @ 2026-10-03T19:12:30+02:00

Added five tests to `GeminiInteractionsChatClientTests` plus a `SequentialHandler`
(per-response status/body/media-type) and `SseFunctionCallPayload`. Design change during
execution: `AgentSession` is abstract and `AIAgent.CurrentRunContext`'s setter is
inaccessible, so the planned direct ambient-context injection is impossible. The tests
drive a real `ChatClientAgent` run (`AsAIAgent` + `CreateSessionAsync` +
`RunAsync`/`RunStreamingAsync`) instead — the production path, with the state bag read
from the session after the run. (Process note: the task frontmatter was moved to
`in-progress` only retroactively; work itself followed the task spec.)

Falsifiability finding worth recording: the initial carried-calls pin (success path) was
NOT falsifiable — with the old write site restored, the test still passed because T02's
pruning removes carried entries on success, masking the regression. The pin was moved to
the failure path (failed requests never prune, so a recorded carried call would survive
and fail the test). Probe-verified: restoring the write site makes the test fail.

## Executor Verification
By: pi coding agent (GLM) @ 2026-10-03T19:12:30+02:00

- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests` → 92/92 passed
  (87 pre-existing + 5 new).
- Falsifiability: re-added the request-content `RememberFunctionName` call →
  `AgentRun_DoesNotRecord_CarriedRequestCalls` FAILS; removed again → suite green.
  `git diff --stat src/` after probe removal: only the intended 86 insertions, 1 deletion.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
