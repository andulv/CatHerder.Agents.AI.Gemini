---
type: plan
description: "Plan 059 - Migrate Gemini Interactions client to the May 2026 steps schema"
status: completed
---

# Plan 059: Gemini Interactions May 2026 Steps Schema Migration

**Status:** completed
**Created:** 2026-05-08T01:49:11+02:00
**Updated:** 2026-05-08T02:03:34+02:00

## Goal

Move this package to the new Gemini Interactions API schema now and remove legacy `outputs` support. All REST calls must opt in with `Api-Revision: 2026-05-20` until the new schema becomes the service default.

## Context / Why

Google announced that the Gemini Interactions API changes from flat `outputs` responses to typed `steps` responses. REST callers can opt in now with `Api-Revision: 2026-05-20`; the new schema becomes default on 2026-05-26 and the legacy schema is removed on 2026-06-08.

Migration source: https://ai.google.dev/gemini-api/docs/interactions-breaking-changes-may-2026

Key changes for this package:
- Response content is read from `steps`; final text is under `model_output.content`.
- Function calls and server-side tool calls/results are top-level step entries.
- Streaming event names change to `interaction.created`, `interaction.status_update`, `step.start`, `step.delta`, `step.stop`, and `interaction.completed`.
- `response_mime_type` is removed; structured output uses polymorphic `response_format` entries.

## Tasks

- [x] **T01** Add regression coverage for the new `steps` response shape, opt-in header, and absence of legacy `outputs` assumptions.
- [x] **T02** Update request serialization for the new `response_format` shape and remove `response_mime_type` usage.
- [x] **T03** Update unary response mapping to consume `steps`, including text, function calls, function results, usage, and built-in tool telemetry.
- [x] **T04** Update SSE reduction/client streaming for the new `step.*` and `interaction.*` event names.
- [x] **T05** Update integration tests to opt in and assert that live responses expose the new steps schema.
- [x] **T06** Run unit/build validation and record the result.

## Acceptance Criteria

- [x] Every outgoing Interactions API request includes `Api-Revision: 2026-05-20`.
- [x] Unit tests fail against legacy `outputs`-only responses and pass against `steps` responses.
- [x] Streaming tests cover `step.start`, `step.delta`, `step.stop`, and `interaction.completed`.
- [x] No package code depends on `outputs` or `response_mime_type`.
- [x] `dotnet build` and offline unit tests pass.

## Notes

- We intentionally support only the new schema from this plan onward.
- Validation passed:
	- `dotnet build --no-restore`
	- `dotnet test tests/CatHerder.Agents.AI.Gemini.UnitTests --no-restore` (30 passed)
	- `dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests --no-restore` (12 passed, 2 skipped by built-in tool opt-in)
- Live integration tests remain credential-gated; built-in tool live tests still require `GEMINI_INTERACTIONS_ENABLE_BUILTIN_TOOLS=true`.
