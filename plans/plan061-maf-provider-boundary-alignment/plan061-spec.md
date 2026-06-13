---
type: plan-spec
description: "Plan 061 - Align Gemini provider with MAF / MEAI IChatClient boundary"
status: ready
created: 2026-06-13T17:10:58+02:00
updated: 2026-06-13T19:52:26+02:00
---
# Plan 061 Spec: MAF / MEAI Provider Boundary Alignment

## 0. Required Context

- `plan-task-standards`
- `.agents/instructions/project.instructions.md`
- `README.md`
- `project-status-roadmap.md`
- `Directory.Build.props`
- Parent `catherder-dev` root `Directory.Packages.props` when this package is
  developed inside the full repository with central package management
- `plans/plan059-interactions-api-schema-change-may2026/plan059.md`
- `plans/plan060-interactions-fail-fast-schema-parsing/plan060-draft.md`
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiSseEventReducer.cs`
- `src/CatHerder.Agents.AI.Gemini/Internal/GeminiUsageMapper.cs`
- `tests/CatHerder.Agents.AI.Gemini.UnitTests/`

## 1. Goal

Make the Gemini Interactions provider behave like a normal Microsoft Agent
Framework / Microsoft.Extensions.AI chat provider at the `IChatClient`
boundary.

Provider-specific Gemini wire details should stay inside the provider.
Consumers should receive standard MEAI `ChatResponse`,
`ChatResponseUpdate`, content items, usage data, and exceptions.

This plan supersedes Plan 060. Plan 060's fail-fast schema parsing work is now
one required outcome of this broader provider-boundary alignment plan.

## 2. Context / Why

The package now targets the current Gemini Interactions `steps` schema. Plan
059 removed legacy `outputs` response support and moved request serialization
to the May 2026 Interactions schema.

Downstream token-accounting and diagnostics depend on provider usage semantics
being coherent, debuggable, and comparable across providers. The Gemini
provider should expose usage data in standard MEAI shapes so consumers do not
need Gemini-specific accounting rules.

Current review found that legacy usage aliases have been removed from runtime
mapping, but the provider still contains overly defensive response and SSE
parsing. Malformed provider data can be skipped, debug-logged, or converted
into empty/partial assistant output. That hides provider/API drift at exactly
the layer that should make it visible.

The package is still in prototype phase. Clean, explicit provider-boundary
behavior is more valuable than broad compatibility with malformed or obsolete
Gemini payloads.

## 3. What We Want To Achieve (Outcomes)

- Gemini-specific JSON shapes, event names, and field names do not leak to
  normal consumers.
- Non-streaming responses produce standard MEAI `ChatResponse` instances with
  equivalent text, content items, usage, model id, response id, and conversation
  id where Gemini provides them.
- Streaming responses produce ordered MEAI `ChatResponseUpdate` values that
  reconstruct to equivalent final chat data for successful interactions.
- OpenTelemetry behavior follows Microsoft.Extensions.AI / MAF canonical
  provider patterns, especially OpenAI Responses behavior, rather than
  Gemini-specific ad hoc spans or events.
- Current Interactions usage fields map to `UsageDetails` with no legacy alias
  fallback.
- Streamed usage follows MEAI summable semantics: either additive partial
  usage values or one final usage value, not ambiguous cumulative snapshots.
- Missing optional data may be absent.
- Missing required current-schema data throws with useful context.
- Invalid current-schema data throws instead of being interpreted as success.
- Function calls with missing required ids, names, or invalid final arguments
  fail visibly instead of disappearing.
- Malformed SSE JSON frames fail the stream instead of being ignored.
- Unsupported caller options, including unknown `ChatResponseFormat`
  variants, fail visibly instead of silently degrading.
- SSE negotiation failure does not silently retry as non-streaming by default.
- Legacy `outputs` support and legacy GenerateContent usage aliases are not
  reintroduced.

## 4. Key Principles / Constraints

- Support only the current Gemini Interactions `steps` schema.
- Keep package code independent of any downstream application.
- Preserve offline unit tests as the default feedback loop.
- Keep live integration tests credential-gated and skipped by default unless
  explicitly requested.
- Normalize provider-specific wire details inside this provider, not in
  downstream consumers.
- Treat OpenTelemetry as public package behavior. Telemetry shape, source names,
  spans, events, attributes, sensitive-data handling, and tool-call reporting
  should align with Microsoft.Extensions.AI `UseOpenTelemetry`,
  `OpenTelemetryChatClient`, OpenAI Responses, and current GenAI semantic
  conventions.
- Prefer direct parsing helpers over new proxy, dispatcher, or adapter layers.
- Add abstraction only when it removes real complexity or matches an existing
  provider-boundary pattern.
- Do not add legacy field lists, legacy compatibility paths, or tests that
  assert legacy usage-field support.
- Domain accounting validation, such as whether negative token counts are
  meaningful, belongs outside the low-level schema mapper unless MEAI/provider
  contracts require otherwise.
- During repository development, target the MAF / MEAI package version pinned
  for the whole `catherder-dev` solution, including submodules. Do not let the
  Gemini submodule drift to a different boundary contract independently. This
  is a development-time dependency alignment rule, not a runtime dependency on
  the CatHerder application.

## 5. Out of Scope

- Changing the Gemini Interactions wire protocol.
- Reintroducing legacy `outputs` response support.
- Reintroducing legacy GenerateContent usage-token aliases.
- Redesigning the request DTO model without a concrete boundary problem.
- Rewriting downstream application diagnostics storage or projection.
- Changing downstream application token-accounting behavior.
- Implementing Vertex AI authentication.
- Running live Gemini integration tests unless explicitly requested.

## 6. Implementation Notes

Implementation direction only. Create tasks from this spec in a separate
implementation plan.

Start by auditing current behavior against the MAF / MEAI package version
chosen for repository development. Use the reference providers and MEAI
abstraction docs/source as the authority for how `ChatResponse`,
`ChatResponseUpdate`, `UsageContent`, `UsageDetails`, function calls, response
formats, and errors should behave at the boundary.

Audit OpenTelemetry behavior before changing SSE event handling. The provider
should primarily expose standard MEAI chat objects and let the canonical MEAI
OpenTelemetry pipeline emit standard GenAI telemetry. Any Gemini-owned
`ActivitySource`, span, event, or attribute should be justified by a gap in the
canonical pipeline and should follow Microsoft provider naming and semantic
convention patterns. Do not translate raw Gemini SSE event names directly into
public telemetry unless that is how the target Microsoft providers behave.

OpenTelemetry audit result, 2026-06-13:

- Target the solution-pinned package boundary: `Microsoft.Extensions.AI`
  `10.7.0`, `Microsoft.Agents.AI` `1.10.0`, and related package versions from
  the root `Directory.Packages.props`.
- `OpenTelemetryChatClient` is a delegating MEAI middleware added with
  `UseOpenTelemetry(...)`. The provider should expose correct `IChatClient`
  data; application composition decides whether telemetry is enabled.
- `OpenTelemetryChatClient` reads provider metadata from
  `ChatClientMetadata`, starts canonical `chat` client activities, records
  GenAI span attributes and metrics, and reads response metadata and
  `UsageDetails`.
- The current MEAI implementation records chat request/response data as GenAI
  attributes and metrics. It does not translate provider SSE frame names into
  public `ActivityEvent` values.
- Sensitive raw inputs, outputs, tool definitions, function-call arguments,
  function-call results, and additional provider properties are gated by
  `EnableSensitiveData` / `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`.
- MEAI function-invocation telemetry creates `execute_tool` activities for
  application-invoked `AIFunction` calls. Arguments and results are also
  sensitive-data gated.
- Therefore the Gemini provider should not emit default public OpenTelemetry
  spans or attributes for raw Gemini SSE event types. It should map provider
  data into standard MEAI content, response metadata, usage, and exceptions.
- The current Gemini-owned built-in-tool `ActivitySource` does not fit this
  pattern. It emits default `execute_tool` spans for provider-side built-in
  tool observations and always records raw arguments/results. Remove it or gate
  it behind an explicit non-default diagnostic option. The preferred plan
  outcome is removal from the standard provider path.

Keep current usage mapping direction:

- `total_input_tokens` -> `UsageDetails.InputTokenCount`
- `total_output_tokens` -> `UsageDetails.OutputTokenCount`
- `total_tokens` -> `UsageDetails.TotalTokenCount`
- `total_cached_tokens` -> `UsageDetails.CachedInputTokenCount`
- `total_thought_tokens` -> `UsageDetails.ReasoningTokenCount`
- `total_tool_use_tokens` -> `UsageDetails.AdditionalCounts`

Missing usage fields should return `null` values. Invalid JSON values should
throw through parsing. Unknown usage fields should not be copied into
`AdditionalCounts` unless explicitly classified as current-schema summable
fields.

Classify every permissive parser branch as one of:

- intentionally optional provider data;
- unknown noncritical additive event;
- malformed current-schema data;
- unsupported caller request.

Malformed current-schema data and unsupported caller requests should throw.
Unknown noncritical additive events may be tolerated only after an explicit
decision.

Streaming fallback decision: `GetStreamingResponseAsync` should fail on SSE
negotiation failure by default. A non-streaming retry may be retained only as
an explicit opt-in provider option, disabled by default. Silent fallback can
hide provider/API drift, double-bill non-idempotent requests, and return a
different answer than the attempted stream.

Provider error mapping decision:

- Match Microsoft.Extensions.AI OpenAI Chat / Responses behavior.
- Transport, authentication, HTTP non-success, invalid request, SDK failure,
  and SSE negotiation failure should throw.
- Malformed current-schema provider data should throw.
- A well-formed provider error inside an otherwise successful response or
  already-established SSE stream should produce MEAI `ErrorContent`.
- Model refusals should produce `ErrorContent` with `ErrorCode = "Refusal"`.
- Content filtering, truncation, and normal completion reasons should map to
  `ChatFinishReason` where the provider supplies equivalent finish/status data.
- For Gemini specifically, non-streaming HTTP non-success should continue to
  throw `GeminiApiException`; a well-formed streaming `status:error` event after
  SSE is established should produce `ErrorContent` with provider code/details
  where available.

Malformed provider data exception decision:

- Add a public `GeminiProtocolException : InvalidOperationException` for
  provider contract violations after a request has succeeded or an SSE stream
  has been established.
- Keep `GeminiApiException : HttpRequestException` for HTTP/API failures where
  the provider rejects the request or returns non-success HTTP status.
- Use `GeminiProtocolException` for malformed SSE JSON frames, known events
  missing required fields, invalid required field types, malformed successful
  non-streaming `steps` data, invalid final function-call arguments, duplicate
  or mismatched built-in tool call/result events, and streams ending with
  incomplete required state.
- Wrap lower-level parsing exceptions such as `JsonException` as inner
  exceptions. Do not expose random `JsonException`, `FormatException`, or
  `InvalidOperationException` as the intended package boundary contract for
  Gemini protocol drift.
- Do not store full raw provider payloads in this exception by default, because
  payloads may contain user text, model output, tool arguments, or other
  sensitive data.
- Include safe metadata when available: operation name, SSE event type, JSON
  field/path, response id, model id, and inner exception.

Suggested implementation sequence after the spec is ready:

1. Confirm the `catherder-dev` solution-level MAF / MEAI package version and
   document the boundary behavior to target for repository development.
2. Finish current-schema usage mapping tests, including streaming usage
   reconstruction.
3. Make non-streaming response parsing fail-fast for required schema.
4. Make SSE frame and known event parsing fail-fast for required schema.
5. Make final streamed function-call/built-in-tool finalization fail when
   required data never becomes complete.
6. Align response-format handling with MEAI/provider expectations.
7. Remove default Gemini-owned built-in-tool OpenTelemetry spans. Preserve
   built-in tool call/result data only as standard MEAI informational content
   unless a later explicit diagnostic option is justified.
8. Review function call, function result, thought/reasoning, and built-in tool
   content mapping for MEAI equivalence.
9. Remove default streaming fallback, or gate it behind an explicit opt-in
   provider option.
10. Add focused boundary tests proving valid current-schema behavior still
   passes and malformed current-schema behavior fails.

## 7. Open Questions

None. Downstream application diagnostics projection, storage, and token
accounting changes are out of scope for this package plan. This plan may
verify only package-local behavior: standard MEAI response/update/content/usage
objects, exceptions, and package-owned telemetry behavior.

## A. Current Review Findings From Plan 060

No runtime source references to legacy usage-token fields remain under `src/`
after the current usage cleanup. Existing `outputs` and `response_mime_type`
references are in tests/docs that assert old schema behavior, not runtime
compatibility code.

Relevant defensive paths still to classify and fix:

- `GeminiInteractionsChatClient.MapInteractionToChatResponse()` skips
  non-object steps.
- `GeminiInteractionsChatClient.MapStep()` returns on missing step type and
  silently ignores unknown step types.
- `GeminiInteractionsChatClient.MapModelOutputStep()` returns on missing
  `model_output.content`, skips non-object content blocks, and silently ignores
  unknown content block types.
- `GeminiInteractionsChatClient.AddFunctionCallContent()` returns when a
  function call is missing id or name.
- `GeminiInteractionsChatClient.ProcessSseFrame()` ignores non-object or
  malformed JSON SSE payloads.
- `GeminiInteractionsChatClient.MapResponseFormat()` maps unsupported
  `ChatResponseFormat` values to `null`.
- `GeminiSseEventReducer` returns when required event structure is missing,
  such as missing `index`, missing `delta`, or missing delta `type`.
- `GeminiSseEventReducer` ignores unsupported delta types.
- `GeminiSseEventReducer.GetFunctionArguments()` treats invalid accumulated
  function-call arguments as incomplete and returns null, even during final
  flush paths.
- `GeminiSseEventReducer.TryGetIndex()` and `TryGetString()` swallow invalid
  values and return false/null.

## B. Expected End State

Consumers should not need Gemini-specific token accounting, response parsing,
or defensive interpretation at the `IChatClient` boundary.

Consumers should be able to treat Gemini like other MAF / MEAI providers for
chat responses, streaming updates, content items, usage, and errors. Downstream
diagnostics projection and storage remain separate application concerns.
