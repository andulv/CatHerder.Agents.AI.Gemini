---
created: 2026-10-01T14:05:00+02:00
updated: 2026-10-01T14:05:00+02:00
---
# Plan 063 Prompt

## Original prompt

NB: For Gemini, we have created the MEAI adapter package /home/anders/source/agent/catherder-dev/submodules/CatHerder.Agents.AI.Gemini and are the maintainers of it. If there are issues with parameters there, it might be our package and we want to fix it there. If functionality/conventions in Agents.AI.Gemini deviates from other MEAI adapters, we want to refactor to be like the others.

## Interpreted prompt

The Gemini Interactions adapter is first-party. Its parameter handling must follow MEAI
adapter conventions. Two known deviations in `GeminiInteractionsChatClient`:
1. `MapThinkingLevel` reads `ChatOptions.AdditionalProperties["reasoning.effort"]` (a
   CatHerder-private key) instead of relying on the typed `ChatOptions.Reasoning` for
   anything the typed model can express.
2. `reasoning.enabled` (off) is not mapped at all — there is no way to disable thinking per
   request where the model supports it (Gemini generation config thinking level "off"/none).

Align the adapter: typed `Reasoning.Effort` first (with the raw AdditionalProperties value
only as an extension for levels the typed enum lacks, mirroring how OpenAI's connector treats
RawRepresentationFactory-provided values), and map an explicit off switch to the Interactions
generation config.
