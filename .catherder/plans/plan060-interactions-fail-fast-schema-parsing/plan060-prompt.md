Review Gemini project to see if there is any other usage of "legacy fields" or
any other usage of overly defensive programming.

Findings from review:

- No legacy Gemini usage-token aliases remain in package source after the recent
  cleanup. Current usage mapping reads only current Interactions usage fields:
  `total_tokens`, `total_input_tokens`, `total_output_tokens`,
  `total_cached_tokens`, `total_thought_tokens`, and
  `total_tool_use_tokens`.
- Existing `outputs` and `response_mime_type` references are in tests/docs that
  assert the old schema is unsupported, not in runtime compatibility code.
- Remaining risk is not legacy usage fields. It is overly defensive parsing in
  current Interactions response/SSE handling: malformed provider data is often
  ignored, logged at debug, or converted into empty/partial assistant responses.

Create a plan to make Gemini Interactions response and SSE parsing fail fast for
current-schema violations. Prompt and spec only for now. Do not create an
implementation plan or task files yet.
