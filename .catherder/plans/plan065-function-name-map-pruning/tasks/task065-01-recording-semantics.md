---
type: task
description: "Task 065-01 — record only response/stream calls; document why the map exists and the transcript-lookup invariant"
status: implemented
created: 2026-10-03T18:50:52+02:00
updated: 2026-10-03T18:58:40+02:00
---
## Required Context
Load and follow these skills:
- `plan-task-standards`

Resources:
- `plan065-spec.md` (outcomes 1 and 4, implementation notes)
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`

## Objective

Stop recording calls that arrive as request content, and document the map so its
existence and pruning rules are not second-guessed later.

## Scope

Included:
- Remove the `RememberFunctionName(functionCall.CallId, functionCall.Name)` call in
  `MapTurnContent`'s `FunctionCallContent` case (`:509`).
- Add a comment at the removal site stating the invariant: any result whose call is
  present in the same request resolves via `TryResolveFunctionNameFromTranscript`, so
  only response/stream-borne calls need the map.
- Add/extend the doc comment on `FunctionNamesByCallIdStateKey` (and `ResolveFunctionName`)
  stating: `function_result.name` is required in practice (live API rejects name-less
  results with HTTP 400 `function_response.name: Name cannot be empty`, 2026-05-20
  revision) although the published reference marks it optional; entries are pruned only
  after the request carrying the result completed, so retries and restarts keep working.
Excluded:
- Pruning logic (T02), tests (T03/T04).

## Steps

1. Edit `MapTurnContent`: remove the `RememberFunctionName` call in the
   `FunctionCallContent` case; add the invariant comment.
2. Add the rationale doc comments on `FunctionNamesByCallIdStateKey` and
   `ResolveFunctionName`.
3. `dotnet build` must stay clean.

## Verification

- `grep -n "RememberFunctionName" src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
  shows no call inside `MapTurnContent` (only response/stream sites and the helper).
- The invariant comment appears at the former write site; the 400 rationale appears on
  `FunctionNamesByCallIdStateKey` and `ResolveFunctionName`.
- `dotnet build` exits 0.
- Manual check: no behavior change in existing tests (they exercise transcript and
  AdditionalProperties fallbacks, which are untouched).

---

Everything above this line is the task specification. Everything below is the
execution record.

# Execution

## Executor Notes
By: pi coding agent (GLM) @ 2026-10-03T18:58:40+02:00

Removed the `RememberFunctionName` call in `MapTurnContent`'s `FunctionCallContent` case
and added the transcript-lookup invariant comment. Added the rationale doc comment on
`FunctionNamesByCallIdStateKey` (400 text, record-only-response/stream, prune-after-
success, state-bag-only) and completed `ResolveFunctionName`'s doc comment (resolution
order + 400 rationale). Note: an intermediate edit briefly dropped the const declaration
and the method signature; both were restored in the same session before building — final
file verified by build + grep.

## Executor Verification
By: pi coding agent (GLM) @ 2026-10-03T18:58:40+02:00

- `dotnet build` → 0 errors, 0 warnings.
- `grep -n "RememberFunctionName" ...` → only the two helpers (:652, :672/:676) and the
  response/stream sites (:1152, :1287); none inside `MapTurnContent`.
- Invariant comment present at the former write site; 400 rationale on the state key and
  `ResolveFunctionName`.
- Existing tests untouched this task; full unit run happens in T02/T03 verification.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
