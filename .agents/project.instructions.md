---
type: project
description: "CatHerder.Agents.AI.Gemini project-specific instructions"
alwaysApply: true
---
# Project Instructions - CatHerder.Agents.AI.Gemini

This repository is a preview .NET package for the Google Gemini Interactions API. It exposes a Microsoft.Extensions.AI `IChatClient` and Microsoft Agent Framework `ChatClientAgent` convenience APIs.

Read first:
- [catherder.instructions.md](catherder.instructions.md)
- [../project-status-roadmap.md](../project-status-roadmap.md)

## Scope
- Keep package code independent of the main CatHerder application; no `CatHerder.*` project dependencies beyond this package namespace.
- Public low-level Gemini request/response DTOs stay internal unless there is a concrete package API need.
- Preserve offline unit tests as the default feedback loop; live tests must remain credential-gated and skipped by default.
- Treat the Gemini Interactions API as preview and schema-evolving; prefer explicit compatibility choices over relying on defaults.

## Commands
From the repository root:
- Build: `dotnet build`
- Unit tests: `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests`
- Pack: `dotnet pack src/CatHerder.Agents.AI.Gemini`
- Live tests: `GOOGLE_API_KEY=... GEMINI_INTERACTIONS_MODEL=... dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests`
