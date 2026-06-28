# Reference Layout Notes

**Decision timestamp:** 2026-05-06T17:29:58+02:00

## Microsoft.Agents.AI.OpenAI

Observed local reference: `/home/anders/source/agent/agent-framework/dotnet/src/Microsoft.Agents.AI.OpenAI`.

Patterns to mimic:

- Focused package project under `src/Microsoft.Agents.AI.OpenAI`.
- Public extension methods live under `Extensions/`.
- Chat-client helpers live under `ChatClient/`.
- Unit tests live in a separate `Microsoft.Agents.AI.OpenAI.UnitTests` project.
- Integration tests are separate projects such as `OpenAIResponse.IntegrationTests` and use fixtures/conformance-style `ChatClientAgent` scenarios.
- Public ergonomics center on converting provider clients into `IChatClient` and `ChatClientAgent`.

Patterns not copied:

- Microsoft copyright/license headers.
- Microsoft package IDs/namespaces.
- OpenAI SDK-specific raw representation support.
- Microsoft repo shared props/import infrastructure.

## Microsoft.Agents.AI.Anthropic

Observed local reference: `/home/anders/source/agent/agent-framework/dotnet/src/Microsoft.Agents.AI.Anthropic`.

Patterns to mimic:

- Thin provider package with public extension methods.
- `AsAIAgent` overloads that accept instructions, name, description, tools, client factory, logger factory, and services.
- Live integration tests use explicit fixtures and skip behavior for local-only provider execution.

Patterns not copied:

- Anthropic SDK dependency model.
- Beta-service extension split unless Gemini Interactions gains separate API variants.

## Package Shape Chosen

```text
packages/CatHerder.Agents.AI.Gemini/
  CatHerder.Agents.AI.Gemini.slnx
  Directory.Build.props
  README.md
  LICENSE
  src/CatHerder.Agents.AI.Gemini/
  tests/CatHerder.Agents.AI.Gemini.UnitTests/
  tests/CatHerder.Agents.AI.Gemini.IntegrationTests/
```

The package keeps the currently working raw-HTTP Interactions API implementation and adds Microsoft-style extension methods around it.
