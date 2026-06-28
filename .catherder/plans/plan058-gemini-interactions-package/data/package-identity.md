# Package Identity

**Decision timestamp:** 2026-05-06T17:29:58+02:00

## Decision

- Repository/workspace folder: `packages/CatHerder.Agents.AI.Gemini`
- Solution: `CatHerder.Agents.AI.Gemini.slnx`
- NuGet package ID: `CatHerder.Agents.AI.Gemini`
- Root namespace: `CatHerder.Agents.AI.Gemini`
- Main public type: `GeminiInteractionsChatClient`
- Title: `CatHerder Agent Framework Gemini`
- Description: `Microsoft Agent Framework and Microsoft.Extensions.AI chat client for the Google Gemini Interactions API.`
- Tags: `AI`, `Agents`, `Gemini`, `Google`, `Microsoft.Extensions.AI`, `Microsoft.AgentFramework`, `IChatClient`
- License: MIT placeholder for local package readiness; final publisher approval still required before NuGet publish.
- Initial version: `0.1.0-preview.1`
- Publish status: preview until Gemini Interactions API and Microsoft Agent Framework dependency compatibility are proven in live use.

## Rationale

`CatHerder.Agents.AI.Gemini` avoids the reserved/confusing `Microsoft.*` namespace while still making the relationship to `Microsoft.Agents.AI.OpenAI` and `Microsoft.Agents.AI.Anthropic` readable. Keeping the workspace under `packages/` gives us a standalone solution and packable package without creating files outside the current CatHerder project root during this execution.

## Deferred Before Public Publish

- Confirm final NuGet owner/account.
- Confirm final repository URL once the package is split or mirrored to GitHub.
- Replace placeholder package icon/metadata if desired.
