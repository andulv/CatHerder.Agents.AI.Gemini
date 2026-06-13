---
created: 2026-06-13T17:10:58+02:00
updated: 2026-06-13T19:52:26+02:00
---
# Plan 061 Prompt

## Original prompt

The original verbatim request was not captured in this file when the draft was
created. The preserved source text from the draft prompt follows.

## Interpreted prompt

We maintain the Gemini Interactions provider for Microsoft Agent Framework / Microsoft.Extensions.AI.

Review and plan how to make the Gemini provider behave as similarly as possible to the reference MAF / MEAI providers at the `IChatClient` boundary.

The goal is not to make Gemini's wire protocol look like another provider. The goal is to normalize provider-specific details inside the Gemini provider so consumers receive standard MEAI chat responses, streaming updates, content items, usage data, and errors.

## Context

Recent CatHerder token accounting work depends on provider usage semantics being coherent and debuggable.

Research found:

- MAF / MEAI reference behavior reconstructs final usage by summing streamed `UsageContent` values.
- CatHerder's chat UI should use chat/session data only.
- CatHerder diagnostics should use telemetry data only.
- The two data sources should remain separate so they can later be compared.
- Gemini usage mapping should use current Gemini Interactions API fields only.
- Legacy usage aliases and broad defensive parsing are not wanted.
- Invalid provider schema should fail loudly instead of being hidden.

Existing Plan 060 covers a narrower cleanup: fail-fast schema parsing and removal of unsupported legacy/defensive code. This plan should include or supersede that work as part of broader provider-boundary alignment.

## Desired Output

Create a draft specification only. Do not create implementation tasks yet.

The spec should:

- Define what "aligned with MAF / MEAI providers" means for this Gemini provider.
- Identify current provider behavior that diverges from that goal.
- State which divergences should be fixed in the provider.
- State which divergences are legitimate Gemini wire-protocol differences and should remain internal.
- Keep CatHerder application changes out of scope unless they are needed to verify provider behavior.
- Include open questions that must be answered before implementation planning.
