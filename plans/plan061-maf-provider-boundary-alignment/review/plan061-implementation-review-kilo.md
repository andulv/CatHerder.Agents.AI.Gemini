---
type: review
reviewer: kilo
date: 2026-06-13T20:04:28+02:00
plan: 061
status: approved
---
# Plan 061 Implementation Review — kilo

## Summary

Overall: Approved. This is a well-crafted plan. The spec is thorough, the task decomposition is logical, and the plan directly addresses the stated goals (fail fast, no swallowed exceptions, no silent fallback, MAF/MEAI-aligned boundary). Every defensive path listed in the spec's Appendix A was verified against the current source code and all findings are accurate.

## Findings

1. **`GeminiSseNegotiationException` visibility gap** — severity: medium, location: spec §6 / T05. The spec defines `GeminiApiException` (HTTP/API failures) and `GeminiProtocolException` (protocol violations after success), but doesn't explicitly classify SSE negotiation failures. Currently `GeminiSseNegotiationException` is a private nested class in `GeminiInteractionsChatClient.cs:1053-1064`. The spec says SSE negotiation failure should throw (good), but T05 should clarify whether this exception stays internal or becomes public. Given the "fail fast and loud" principle, recommend making it public or folding it into `GeminiApiException` since SSE negotiation failures are fundamentally HTTP/transport failures.

2. **`TryParseGeminiError` bare catch** — severity: low, location: `GeminiInteractionsChatClient.cs:1008-1011`. The bare `catch { return null; }` swallows all exceptions during error response parsing. This is in the non-success HTTP path which already throws `GeminiApiException`, so it only affects error message quality. Not a behavioral correctness issue, but inconsistent with the fail-fast principle. Consider whether T02 or T06 should address it.

3. **`GeminiUsageMapper.Map` throws `FormatException`** — severity: low, location: `GeminiUsageMapper.cs:17`. Non-object usage throws `FormatException` instead of the planned `GeminiProtocolException`. T02 covers this, but the task description could call it out explicitly since it's already a throw (just the wrong exception type).

## Spec Verification

Cross-referenced every defensive path in spec Appendix A against current source:

- `MapInteractionToChatResponse` skips non-object steps (line 767-769) ✓
- `MapStep` returns on missing type and ignores unknown types (lines 807-809, 839) ✓
- `MapModelOutputStep` returns on missing content, skips non-object blocks, ignores unknown types (lines 844-846, 851-853) ✓
- `AddFunctionCallContent` returns on missing id/name (lines 898-900) ✓
- `ProcessSseFrame` ignores non-object and malformed JSON payloads (lines 956-966) ✓
- `MapResponseFormat` maps unknown formats to `null` (line 688) ✓
- `GeminiSseEventReducer` returns on missing index/delta/type (lines 104-106, 153-163) ✓
- `GetFunctionArguments` treats invalid arguments as incomplete, returns null (lines 326-335) ✓
- `TryGetIndex`/`TryGetString` swallow invalid values (lines 452-470, 475-485) ✓

## Structure Verification

- Validator: Clean (0 errors, 0 warnings, 10 files checked).
- Task ordering: T01 first (exception contract) is correct. T02/T03 parallelism caveat is honest. T07 as consolidation is right.
- Acceptance criteria: Complete and directly traceable to spec outcomes.
- Task verification: Each task has concrete `dotnet test` commands and `rg` searches.

## Recommendation

The plan is ready for execution. Finding #1 is the only item that should be addressed before T05 starts — the executor should make an explicit decision about `GeminiSseNegotiationException` visibility and document it in the task's execution record. Findings #2 and #3 are minor and can be handled during execution.
