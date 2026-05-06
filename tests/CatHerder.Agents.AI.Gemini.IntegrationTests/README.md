# Gemini Interactions Integration Tests

These tests call the real Gemini Interactions API and are skipped unless configured.

## Required Settings

Set these as environment variables or user secrets:

- `GOOGLE_API_KEY`
- `GEMINI_INTERACTIONS_MODEL`

Example:

```bash
GOOGLE_API_KEY=... GEMINI_INTERACTIONS_MODEL=gemini-3.1-flash-lite-preview \
  dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests
```

## Optional Settings

- `GEMINI_INTERACTIONS_BASE_URL` defaults to `https://generativelanguage.googleapis.com/v1beta/`.
- `GEMINI_INTERACTIONS_ENABLE_BUILTIN_TOOLS=true` enables live built-in tool smoke tests.

## Notes

These tests may incur provider cost and depend on model availability, quota, network access, and current Interactions API behavior. Normal CI should run the unit test project only unless a secure live-test workflow is configured.
