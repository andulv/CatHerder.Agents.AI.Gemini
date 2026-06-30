---
created: 2026-06-30T22:48:21+02:00
updated: 2026-06-30T23:14:33+02:00
---
# Plan 062 Prompt

## Original prompt

Multiple issues discovered in the Gemini Interactions chat client during
CatHerder plan 109 (model parameter selection) and live testing. These are
independent fixes bundled into one plan because they all concern the
`GeminiInteractionsChatClient` and its request/response handling.

## Interpreted prompt

### Issue 1 — Reasoning config not sent to the API

The client silently ignores `ChatOptions.Reasoning` and reasoning-related
`ChatOptions.AdditionalProperties`. When a caller sets reasoning effort
(e.g. `ReasoningOptions.Effort = ReasoningEffort.High` or
`AdditionalProperties["reasoning.effort"] = "high"`), the client never
includes a thinking config in the Interactions API request.

- `MapGenerationConfig` (`GeminiInteractionsChatClient.cs:655-670`) only reads
  `Temperature`, `MaxOutputTokens`, `TopP`, `TopK`, and `StopSequences`.
- `GeminiInteractionGenerationConfig` (`GeminiInteractionsRequestModels.cs:64-75`)
  has no thinking/reasoning field, and the top-level `GeminiInteractionRequest`
  (lines 5-22) has no `thinkingConfig` field either.
- The Gemini Interactions API supports a `thinkingConfig` field (see
  https://ai.google.dev/gemini-api/docs/thinking). Effort levels map to `low`,
  `medium`, `high` (and model-specific values like `minimal`).
- `ChatOptions.Reasoning.Effort` is a `Microsoft.Extensions.AI.ReasoningEffort`
  enum (`None`, `Low`, `Medium`, `High`, `ExtraHigh`). Unrepresentable effort
  values arrive via `AdditionalProperties["reasoning.effort"]` as raw strings.

**Fix:** Add thinking config to the request model, and wire it from
`ChatOptions.Reasoning` / `AdditionalProperties` in `MapGenerationConfig` or
the request builder.

### Issue 2 — Standalone SSE error events silently swallowed

When the Gemini Interactions API sends a standalone `event: error` frame
mid-stream (e.g. `{"error":{"message":"...high demand...","code":"api_error"}}`),
the client swallows it and the user sees an empty assistant bubble.

- `GeminiSseEventReducer.Reduce` (`GeminiSseEventReducer.cs:24-63`) has cases
  for `interaction.created`, `interaction.status_update`, `step.start`,
  `step.delta`, `step.stop`, `interaction.completed`, `done` — but **no
  `case "error":`**. The `default` branch logs at Debug and returns empty
  updates.
- The reducer DOES handle `status == "error"` inside `interaction.status_update`
  events (`GeminiSseEventReducer.cs:83-104`), correctly emitting `ErrorContent`.
  It just doesn't handle the standalone `event: error` frame.
- CatHerder.Web's error handling is correct (`ChatSession.cs:386-400` catches
  exceptions, `ChatSession.cs:503-521` scans for `ErrorContent`,
  `StreamingContentConverter.cs:21-28` converts it). It just never fires
  because the Gemini client never emits the error.

**Fix:** Add a `case "error":` to `GeminiSseEventReducer.Reduce` that parses
the payload's `error` object (message/code/status) and emits an
`ErrorContent` update. The existing CatHerder.Web error pipeline will surface
it automatically.

### Issue 3 — Truncated responses shown without warning

When max output tokens is too low, Gemini returns `status: "incomplete"` in
the `interaction.completed` event, and the model output is cut short mid-text.
The Gemini client correctly maps this to `ChatFinishReason.Length`
(`GeminiInteractionsChatClient.cs:1166`), but CatHerder.Web does not surface a
truncation warning to the user — it just shows the cut-short text as if it
were complete.

**Root cause:** `GeminiSseEventReducer.HandleInteractionCompleted`
(`GeminiSseEventReducer.cs:263-285`) sets `update.FinishReason = Length`, but
CatHerder.Web's `ChatSession.CompleteAssistantResponse`
(`ChatSession.cs:503-521`) only scans for `ErrorContent` — it ignores
`FinishReason.Length`. No warning, no indicator.

**Fix (submodule side, optional):** The reducer could emit a visible
`TextContent` or `ErrorContent` warning when status is `"incomplete"` so the
user sees "Response truncated (max output tokens reached)". This is a UI
convenience, not strictly a protocol fix — the finish reason is already
correct.

**Fix (CatHerder.Web side):** `ChatSession` could check
`agentResponse.FinishReason == ChatFinishReason.Length` and append a warning
message. This is the more conventional place for UX decisions.

### Issue 4 — Reasoning/thought tokens excluded from output pricing and display

Google bills thought tokens (`total_thought_tokens`) as output tokens. But
CatHerder only counts `total_output_tokens` for pricing and display, excluding
thought tokens. With a response that has `total_output_tokens: 2` and
`total_thought_tokens: 94`, the user sees "2 out" instead of "96 out" and the
price is calculated on 2 tokens instead of 96.

**Root cause:** The Gemini usage mapper correctly separates them:
`GeminiUsageMapper.cs:26` sets `OutputTokenCount = total_output_tokens` (2)
and `GeminiUsageMapper.cs:29` sets `ReasoningTokenCount = total_thought_tokens`
(94). But the pricing path in `ChatSession.cs:528-531` passes only
`LastUsage.OutputTokens` (2) to `CalculateUsageCost`, and
`ModelRecord2.cs:66` prices only those tokens.

**Fix (CatHerder.Web):** The cost calculation should use
`outputTokens + reasoningTokens` for the output token count, since Google
prices both as output. This affects:
- `ChatSession.cs:528-531` — pass combined count to `CalculateUsageCost`
- `ModelRecord2.cs:41` — or change `CalculateUsageCost` to accept a separate
  reasoning token parameter
- The usage display (`ChatStatsStrip`, `ChatTranscriptBuilder`,
  `ApiRequestsPanel`) — should show output tokens including thought tokens, or
  at minimum show both separately so the total is clear

**Note:** This is NOT a Gemini submodule issue — the mapper is correct per the
MEAI convention. The pricing/display logic is in CatHerder.Web and applies to
all providers that separate reasoning tokens (including OpenAI o-series).

### Issue 5 — Thought summaries not requested from the API

By default, the Gemini Interactions API returns only the encrypted
`thought_signature` (a cryptographic signature), not human-readable
`thought_summary` text. Readable thought summaries must be explicitly enabled
by sending `"thinking_summaries": "auto"` in the generation config (source:
https://ai.google.dev/gemini-api/docs/thinking).

Currently `GeminiInteractionGenerationConfig`
(`GeminiInteractionsRequestModels.cs:64-75`) has no `thinking_summaries` field,
and `MapGenerationConfig` (`GeminiInteractionsChatClient.cs:655-670`) never
sets it. So the API never sends readable thought summaries, and the
`TextReasoningContent` pipeline in the reducer
(`GeminiSseEventReducer.cs:192-205`) is never triggered.

CatHerder.Web's reasoning display pipeline is correct and ready
(`ChatSession.cs:472-474` captures `TextReasoningContent`,
`ChatMessageItemView.razor:35-42` renders a collapsible "Reasoning" panel) —
it just never receives any reasoning content to display.

**Fix (Gemini submodule):**
- Add `ThinkingSummaries` (string) to `GeminiInteractionGenerationConfig`.
- Set it to `"auto"` in `MapGenerationConfig` when the model supports thinking
  (i.e. when `ChatOptions.Reasoning` is set or the model is known to support
  reasoning). Alternatively, always send `"auto"` — it is harmless for
  non-thinking models.
- The existing reducer path for `thought_summary` deltas
  (`GeminiSseEventReducer.cs:192-205`) already emits `TextReasoningContent`
  correctly once summaries arrive.

**Note on pricing (relates to issue 4):** Google confirms: "Pricing is based on
the full thought tokens the model needs to generate, despite only the summary
being output from the API." This reinforces that thought tokens must be
included in the output token count for pricing, regardless of whether
summaries are enabled.

## Verification

- `dotnet build` and `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests`
  pass.
- **Issue 1:** Unit test: when `ChatOptions.Reasoning.Effort` is set, the
  serialized request includes the thinking config. Unit test: when
  `AdditionalProperties["reasoning.effort"]` carries a raw string not
  representable by the enum (e.g. `minimal`), the request includes it. Unit
  test: when no reasoning options are set, no thinking config is emitted
  (backward compatible).
- **Issue 2:** Unit test: when a standalone `event: error` SSE frame arrives
  mid-stream, the reducer emits an `ErrorContent` with the message and code.
  Unit test: the stream does not silently complete with an empty response when
  an error frame was received.
- **Issue 3:** Unit test (if submodule-side fix chosen): when
  `interaction.completed` has `status: "incomplete"`, the reducer emits a
  warning `TextContent` or `ErrorContent` in addition to the finish reason.
- **Issue 4:** Requires CatHerder.Web changes — verify pricing includes
  reasoning tokens in the output token count.
- **Issue 5:** Unit test: when thinking is supported, the serialized request
  includes `"thinking_summaries": "auto"` in the generation config. Unit test:
  when a `thought_summary` SSE delta arrives, the reducer emits
  `TextReasoningContent` with the summary text (the existing reducer path is
  correct but currently unreachable without the config flag).

## Context

- All issues discovered during CatHerder plan 109 (model parameter selection)
  and live testing with Gemini models.
- No CatHerder.Web changes needed for either issue — the web layer's error and
  parameter handling is correct; the gaps are in the Gemini submodule.
