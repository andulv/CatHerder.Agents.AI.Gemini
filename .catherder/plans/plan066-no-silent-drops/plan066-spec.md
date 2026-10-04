---
type: plan-spec
description: "Plan 066 - no silent drops: every response part, failure status, stream error and ChatOptions field is mapped or fails visibly; docs match the code; unsupported API features are listed"
status: draft
created: 2026-10-04T04:15:00+02:00
updated: 2026-10-04T04:15:00+02:00
---
# Plan 066 Spec — No Silent Drops

## 0. Required Context

- `plan-task-standards`; this submodule's `.agents/` instructions.
- [`plan066-prompt.md`](plan066-prompt.md)
- [`data/audit-2026-10-04.md`](data/audit-2026-10-04.md): the findings this plan addresses,
  with evidence.
- `src/CatHerder.Agents.AI.Gemini/GeminiInteractionsChatClient.cs` (`MapStep`,
  `MapModelOutputStep`, `MapFinishReason`, `MapTools`, `MapChatOptionsToGenerationConfig`,
  `LogThoughtSummary`), `Internal/GeminiSseEventReducer.cs`, `Internal/GeminiUsageMapper.cs`,
  `README.md`, `project-status-roadmap.md`.
- Precondition: the uncommitted working-tree changes of 2026-10-04 are committed first.
  These are: input media is mapped or throws (assistant-turn audio, PDF and audio from
  function results sent as `user_input`, `HostedFileContent` throws), and the pre-flight
  size check (`MaxInlineRequestBytes`, `GeminiRequestSizeExceededException`) is removed.
- Superseded: `plan060-interactions-fail-fast-schema-parsing` (old-format draft, never
  completed). This plan answers its open questions.
- Parent context (read-only): CatHerder turns a thrown exception from the chat client into a
  failed, retryable turn. An `ErrorContent` update is only shown as a line in the chat, and
  the turn is still recorded as completed.

## 1. Goal

The client never loses data or failures without a trace. Every part of a response, every
interaction status, every stream error and every `ChatOptions` field is either mapped or
fails visibly. The README describes what the client does, including its limits.

## 2. Context / Why

On 2026-10-04 the parent repo found that a chat with an audio attachment failed on every
later turn, and that media was dropped without a trace. Fixing the input side showed the
same pattern on the response and streaming side (see the audit). Unknown response types are
skipped. `failed` / `cancelled` become a `null` finish reason. Stream errors become content
instead of failures. Some options are ignored. A caller cannot tell that something went
wrong, and new API features vanish instead of failing loudly. The README now claims
"nothing is dropped silently", which is true only for input.

## 3. What We Want To Achieve (Outcomes)

- An unknown response step type or `model_output` content type no longer disappears. It
  fails visibly (see question 1).
- A stream `error` event or `status: error` makes the call fail with an exception. The
  partial output received before the error is kept, as it is today for other stream
  failures.
- `failed` and `cancelled` statuses (and any unknown terminal status) are reported as a
  failure, not as a normal stop with a `null` finish reason (see question 2).
- Unknown SSE event types are tolerated only if they are lifecycle events that carry no
  output, and are logged at Warning level, not Debug. `interaction.in_progress` and
  `interaction.requires_action` are handled if a live check shows the API sends them.
- `ChatOptions.ToolMode` maps to `tool_choice`, `Seed` maps to `seed`, and
  `AllowMultipleToolCalls` is mapped or rejected. Options without an API equivalent
  (`FrequencyPenalty`, `PresencePenalty`) throw `NotSupportedException` when set.
- Non-streaming thoughts become `TextReasoningContent`, as in streaming.
- README and roadmap match the code. The README has a "Limits" section that lists what is
  not mapped, and the "nothing is dropped" sentence matches the real behaviour.
- The roadmap lists the unsupported Interactions API features from audit §C as candidate
  plans.
- Unit tests prove each case. A gated live test covers stream errors or status events where
  the live API can produce them.

## 4. Key Principles / Constraints

- Map it, or fail loudly. A Debug log line does not count as visible.
- Use exceptions for failures, not `ErrorContent` updates. Callers (MEAI, MAF, CatHerder)
  treat exceptions as failures.
- Do not guess unknown shapes: unknown content is not turned into text or skipped.
- Malformed known data keeps failing fast (the plan060 work that is already in the code).
- Offline unit tests stay the default check. Live tests stay credential-gated.
- No change to the parent CatHerder application is needed. The parent only picks up the
  submodule bump.

## 5. Out of Scope

- Citations / `annotations` (audit A, medium): a feature with its own model (how sources
  reach MEAI and the UI). Separate plan, see question 4.
- Per-modality usage and grounding counts (audit A, low).
- The unsupported API features in audit §C (Files API, `file_search`, MCP, Computer Use,
  agents, background, GET/DELETE/cancel, audio/image/video output, video input,
  `store` / data retention). Each is listed in the roadmap and planned separately.
- Changes to the parent repo.

## 6. Implementation Notes

- Response mapping: add a final branch in `MapStep` and `MapModelOutputStep` (decided by
  question 1). Streaming: the same rule in `HandleStepStart` and the delta `default` branch.
  Lifecycle-only events go through a small known list. Everything else follows question 1.
- Stream errors: `HandleErrorEvent` / `HandleStatusUpdate` throw a `GeminiApiException`
  that carries the code, message and details, instead of adding `ErrorContent`. Check how
  the stream loop surfaces exceptions from the reducer, and keep the partial updates
  already yielded.
- Finish status: decide per status (question 2). Log unknown statuses at Warning level at
  least.
- `tool_choice`: check the wire shape against the reference (`auto` / `any` / `none` /
  `validated`, `allowed_tools`) and a live call. `RequireSpecific` maps to `any` with
  `allowed_tools`.
- Thoughts: reuse the streaming mapping to `TextReasoningContent`.
- Tasks could be split like this: response/stream failures; options mapping; thoughts;
  docs and roadmap.

## 7. Open Questions

1. What should happen to an unknown response step or content type? Options: (a) throw, so
   the turn fails until the adapter learns the type; (b) surface it as a visible
   non-text item (raw JSON in a content item, plus a Warning log) and continue.
   Recommendation: (a). Unknown means the reply may be incomplete, so failing is the honest
   outcome, and Google announces new types in the changelog. (open)
2. How should `failed`, `cancelled` and unknown statuses be reported? Recommendation:
   `failed` throws `GeminiApiException` with the error from the interaction. `cancelled`
   throws `OperationCanceledException` if the caller cancelled, otherwise
   `GeminiApiException`. An unknown terminal status throws. (open)
3. Should unknown lifecycle-only SSE events (no output) keep being tolerated, logged at
   Warning level? Recommendation: yes, for event names without `step` / `content` payloads;
   all others follow question 1. This answers plan060 question 1. (open)
4. Citations (`annotations`): a separate plan, or part of this one? Recommendation: a
   separate plan. MEAI has `CitationAnnotation` on `TextContent`, so the mapping is small,
   but showing sources in CatHerder needs parent work. (open)
5. Should `AllowMultipleToolCalls = false` be mapped or rejected? The Interactions API has
   no direct switch. Recommendation: throw `NotSupportedException` when it is `false`.
   (open)
6. Data retention: the client never sends `store`, so Google keeps every interaction, and
   the client never deletes them. This is in scope only as documentation in this plan.
   Should a parent-repo decision be raised? (open)
