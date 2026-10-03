---
type: plan-data
description: "Plan 065 - investigation of the call-ID-to-name map and the live API experiment (2026-10-03)"
created: 2026-10-03T15:55:00+02:00
updated: 2026-10-03T15:55:00+02:00
---
# Call-ID-to-name map — investigation (2026-10-03)

## 1. The mechanism

`src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`:

- `FunctionNamesByCallIdStateKey` = `catherder.agents.ai.gemini.function_names_by_call_id`
  (`:21`), a `Dictionary<string,string>` in `AIAgent.CurrentRunContext?.Session?.StateBag`.
- `RememberFunctionName` records call id → name for every call the adapter sees:
  - calls sent in a request (`:482`, inside content mapping),
  - `function_call` steps parsed from a response (`:1084`),
  - contents of every streaming update (`:1219` → `RememberFunctionNames`).
- `ResolveFunctionName` (`:528`) resolves the name of a result, in order:
  1. the map (session state, `:555`),
  2. a matching `FunctionCallContent` in the same request's messages (`:578`),
  3. `AdditionalProperties` keys `name` / `function_name` / `functionName` (`:540`),
  and throws `InvalidOperationException` when all three fail (`:552`).

The parent repo (CatHerder) persists the state bag in `continuation.json`
(`data.stateBag`), so the map survives a restart. Example: session `b531e81a2990`
in the parent repo's demo1 workspace has **291 entries (15 711 bytes)** after 3 Gemini
turns — it includes every `chatcmpl-tool-…` call of Fireworks turns carried into the
Gemini session.

## 2. Why the map is needed

Gemini keeps the conversation server-side, chained by `previous_interaction_id`.
MAF then sends only new input. In the function loop, the follow-up request carries
**only** `function_result` contents; the `function_call` steps live in the previous
interaction. Lookup 2 finds nothing; the map is the only source of the name.

Needed for: every tool-loop iteration, retrying results after a failed request, and
requests after a process restart (map restored from `continuation.json`).

## 3. Live experiment (2026-10-03)

Scratch script, real API, model `gemini-3.1-flash-lite-preview`, endpoint
`v1beta/interactions`, `Api-Revision: 2026-05-20`. Prompt triggered 4 parallel calls
(`get_weather`/`get_time` for Bergen and Oslo); the follow-up sent the 4 results with
`previous_interaction_id`.

| Case | Result |
|---|---|
| Results with `name` (control) | 200, correct final answer |
| Results without `name` | **400** `invalid_request`: `GenerateContentRequest.contents[2].parts[N].function_response.name: Name cannot be empty.` (one line per result) |
| Same without `name`, `stream: true` follow-up | **400**, same error |

Conclusion: the Interactions API converts to the `GenerateContent` shape internally,
where the name is required. The published reference
(https://ai.google.dev/api/interactions-api — `name` "(optional)") does not match
actual behavior. The adapter's throw when no name can be resolved is therefore correct.

## 4. Growth

`RememberFunctionName` is called for every call the adapter ever sees and nothing is
removed:

- Calls of carried history (all providers) are recorded on every request that contains
  them — the bulk of the 291 entries.
- Answered calls stay recorded forever, although their name will never be needed again
  (their result went out successfully).

## 5. Rejected alternative

Dropping the name when unknown is not possible (the API rejects it, section 3).
Moving the lookup into the parent repo's log was rejected: it would couple the parent's
Core to a Gemini wire quirk, and the name is needed before the next service call, inside
MAF's function loop, where the parent's history provider is not consulted.
