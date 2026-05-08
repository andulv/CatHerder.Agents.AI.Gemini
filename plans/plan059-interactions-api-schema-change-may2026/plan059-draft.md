---
type: plan
description: "Plan 059 draft - Migrate Gemini Interactions client to the May 2026 steps schema"
status: draft
---

# Plan 059 Draft: Gemini Interactions May 2026 Steps Schema Migration

**Status:** draft
**Created:** 2026-05-08T01:49:11+02:00
**Updated:** 2026-05-08T01:49:11+02:00

## Goal

Move this package to the new Gemini Interactions API schema and stop depending on legacy `outputs` responses before the service default changes.

## Context / Why

Google announced that Gemini Interactions REST responses move from flat `outputs` entries to typed `steps` entries. The client should opt in with `Api-Revision: 2026-05-20`, update unary and streaming response mapping, and use the new structured-output request shape.

## What We Want To Achieve (Outcomes)

- Requests opt in to the May 2026 schema.
- Unary and streaming mappings consume `steps` and new event names.
- Structured output uses `response_format` rather than `response_mime_type`.
- Tests prove that legacy `outputs`-only responses are unsupported.

## Summary Of Work Needed

Add regression coverage, update request serialization, update unary response mapping, update streaming event reduction, add live opt-in schema validation, and run build/test validation.

## Key Principles / Constraints

- Support only the May 2026 `steps` schema from this plan onward.
- Do not keep a legacy `outputs` compatibility path in package code.
- Keep integration tests credential-gated and safe to skip in normal local runs.
- Prefer focused regression tests around request/response schema boundaries.

## Open Questions

- Are live credentials available to validate the raw opt-in schema against the service?
