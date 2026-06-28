---
type: task
description: "Task 061-02 - Make non-streaming response parsing fail fast"
status: implemented
created: 2026-06-13T19:58:10+02:00
updated: 2026-06-13T20:14:53+02:00
---
## Required Context

Load and follow these skills:
- `plan-task-standards`

Read:
- `../plan061-spec.md`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiUsageMapper.cs`
- Existing non-streaming unit tests in `tests/CatHerder.Agents.AI.Gemini.UnitTests/`

## Objective

Make successful non-streaming Interactions responses fail visibly when required
current-schema data is malformed.

## Scope

Included:
- Apply `GeminiProtocolException` to malformed successful response data.
- Require current `steps` schema where the provider contract requires it.
- Preserve optional data as optional.
- Preserve current usage-field mapping with no legacy aliases.
- Add tests for malformed required response shapes.

Excluded:
- SSE parsing.
- Caller option handling.
- CatHerder application behavior.

## Steps

1. Review `MapInteractionToChatResponse`, `MapStep`, `MapModelOutputStep`, and
   related helper methods.
2. Classify every permissive branch as optional, unknown additive, malformed
   current-schema data, or unsupported request.
3. Replace malformed current-schema silent returns/skips with
   `GeminiProtocolException`.
4. Ensure valid current-schema responses still map text, content, ids, model,
   conversation id, and usage.
5. Add tests for missing/non-object steps, missing required step type, malformed
   model output content, invalid function call id/name/arguments, and invalid
   usage object type.
6. Verify legacy `outputs` and legacy usage aliases remain unsupported.

## Verification

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiInteractionsChatClientTests` passes.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiInteractionsChatClientPhase2To4Tests` passes.
- `rg -n "prompt_token_count|candidates_token_count|total_token_count|outputs" src/CatHerder.Agents.AI.Gemini` returns no runtime compatibility path.
- Manual check: successful malformed provider data throws `GeminiProtocolException`, not empty assistant output.

---

Everything above this line is the task specification. Everything below is the execution record.

# Execution

## Executor Notes
By: Codex GPT-5 @ 2026-06-13T20:14:53+02:00

- Converted successful non-streaming malformed current-schema response data to `GeminiProtocolException`.
- Required `steps` to be an array, each step/content block to be an object, and required step/content/function-call fields to be present and typed correctly.
- Required function-call arguments to be a JSON object.
- Converted invalid usage shape/type failures to `GeminiProtocolException`.
- Preserved valid current-schema usage mapping and kept legacy `outputs` / legacy usage aliases unsupported.

## Executor Verification
By: Codex GPT-5 @ 2026-06-13T20:14:53+02:00

- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiInteractionsChatClientTests` passed. Unit tests: 15 passed. Integration test assembly had no matching tests for the filter.
- `dotnet test CatHerder.Agents.AI.Gemini.slnx --filter GeminiInteractionsChatClientPhase2To4Tests` passed. Unit tests: 23 passed. Integration test assembly had no matching tests for the filter.
- `rg -n "prompt_token_count|candidates_token_count|total_token_count|outputs" src/CatHerder.Agents.AI.Gemini` returned no matches.
- Manual check: malformed successful provider data now throws `GeminiProtocolException` instead of producing empty assistant output.

## Reviewer Verification
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>

## Review Notes
By: <agent/model-or-unknown> @ <YYYY-MM-DDTHH:MM:SS+HH:MM>
